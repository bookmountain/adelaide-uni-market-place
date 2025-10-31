import { cn } from '@/lib/utils';
import * as React from 'react';
import { Image, Text, View, type ImageProps, type ImageStyle, type ViewStyle } from 'react-native';

type AvatarProps = React.ComponentProps<typeof View> & {
  image?: ImageProps['source'];
  name?: string;
  size?: number;
};

export function Avatar({ image, name, size = 48, className, style, ...props }: AvatarProps) {
  const initials = React.useMemo(() => {
    if (!name) return 'A';
    return name
      .split(' ')
      .map((part) => part.charAt(0).toUpperCase())
      .slice(0, 2)
      .join('');
  }, [name]);

  return (
    <View
      className={cn('items-center justify-center overflow-hidden rounded-full bg-accent', className)}
      style={[containerStyle(size), style]}
      {...props}>
      {image ? (
        <Image source={image} style={imageStyle(size)} resizeMode="cover" />
      ) : (
        <Text className="text-sm font-semibold text-accent-foreground">{initials}</Text>
      )}
    </View>
  );
}

const containerStyle = (size: number): ViewStyle => ({
  width: size,
  height: size,
  borderRadius: size / 2,
});

const imageStyle = (size: number): ImageStyle => ({
  width: size,
  height: size,
  borderRadius: size / 2,
});
