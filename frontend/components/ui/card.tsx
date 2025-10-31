import { cn } from '@/lib/utils';
import * as React from 'react';
import { Platform, StyleSheet, Text, View, type TextProps, type ViewProps } from 'react-native';

const styles = StyleSheet.create({
  shadow: Platform.select({
    ios: {
      shadowColor: '#1E1A3C',
      shadowOpacity: 0.08,
      shadowRadius: 20,
      shadowOffset: { width: 0, height: 12 },
    },
    android: {
      elevation: 4,
      shadowColor: '#1E1A3C',
    },
    default: {},
  }) as Record<string, unknown>,
});

export function Card({ className, style, ...props }: ViewProps & { className?: string }) {
  return <View className={cn('rounded-3xl border border-border bg-card', className)} style={[styles.shadow, style]} {...props} />;
}

export function CardContent({ className, ...props }: ViewProps & { className?: string }) {
  return <View className={cn('gap-3 px-6 py-5', className)} {...props} />;
}

export function CardHeader({ className, ...props }: ViewProps & { className?: string }) {
  return <View className={cn('gap-2 px-6 pt-6', className)} {...props} />;
}

export function CardFooter({ className, ...props }: ViewProps & { className?: string }) {
  return <View className={cn('flex-row items-center justify-between px-6 pb-6', className)} {...props} />;
}

export function CardTitle({ className, ...props }: TextProps & { className?: string }) {
  return <Text className={cn('text-xl font-semibold text-foreground', className)} {...props} />;
}

export function CardDescription({ className, ...props }: TextProps & { className?: string }) {
  return <Text className={cn('text-sm text-muted-foreground', className)} {...props} />;
}
