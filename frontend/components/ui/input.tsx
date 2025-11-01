import { cn } from '@/lib/utils';
import * as React from 'react';
import { forwardRef } from 'react';
import { Platform, TextInput, type TextInputProps } from 'react-native';

type InputProps = TextInputProps & {
  className?: string;
  error?: boolean;
};

const BASE_STYLE = 'h-12 rounded-2xl border bg-card px-4 text-base text-foreground';
const ERROR_STYLE = 'border-destructive';
const NORMAL_STYLE = 'border-input';

export const Input = forwardRef<TextInput, InputProps>(
  ({ className, error = false, placeholderTextColor, style, ...props }, ref) => {
    const mergedStyle = React.useMemo(() => {
      if (Platform.OS === 'ios') {
        return [
          {
            shadowColor: '#836BFF',
            shadowOpacity: 0.1,
            shadowRadius: 12,
            shadowOffset: { width: 0, height: 6 },
          },
          style,
        ];
      }

      if (Platform.OS === 'android') {
        return [{ elevation: 1 }, style];
      }

      return style;
    }, [style]);

    return (
      <TextInput
        ref={ref}
        className={cn(BASE_STYLE, error ? ERROR_STYLE : NORMAL_STYLE, className)}
        placeholderTextColor={placeholderTextColor ?? 'rgba(82, 80, 112, 0.6)'}
        selectionColor="rgba(131, 107, 255, 0.75)"
        style={mergedStyle}
        {...props}
      />
    );
  },
);

Input.displayName = 'Input';
