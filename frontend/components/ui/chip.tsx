import { cn } from '@/lib/utils';
import * as React from 'react';
import { Pressable, Text, type PressableProps } from 'react-native';

type ChipProps = Omit<PressableProps, 'children'> & {
  active?: boolean;
  className?: string;
  children: React.ReactNode;
};

export function Chip({ active = false, className, children, ...props }: ChipProps) {
  return (
    <Pressable
      accessibilityRole="button"
      className={cn(
        'rounded-full border px-4 py-2',
        active ? 'border-primary bg-primary/10' : 'border-border bg-secondary',
        className,
      )}
      {...props}>
      <TextLabel active={active}>{children}</TextLabel>
    </Pressable>
  );
}

function TextLabel({ active, children }: { active: boolean; children: React.ReactNode }) {
  return (
    <Text className={cn('text-sm font-medium', active ? 'text-primary' : 'text-muted-foreground')}>
      {children}
    </Text>
  );
}
