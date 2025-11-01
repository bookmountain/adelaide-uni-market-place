import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Text } from '@/components/ui/text';
import { Link, Stack, router } from 'expo-router';
import * as React from 'react';
import { SafeAreaView, View } from 'react-native';

export default function RegisterScreen() {
  const [fullName, setFullName] = React.useState('');
  const [email, setEmail] = React.useState('');
  const [password, setPassword] = React.useState('');
  const [confirmPassword, setConfirmPassword] = React.useState('');

  return (
    <>
      <Stack.Screen options={{ headerShown: false }} />
      <SafeAreaView className="flex-1 bg-background">
        <View className="flex-1 justify-between px-6 py-10">
          <View className="items-center gap-6">
            <View className="h-16 w-16 items-center justify-center rounded-2xl bg-primary/10">
              <Text className="text-2xl font-semibold text-primary">AUM</Text>
            </View>
            <View className="gap-3">
              <Text variant="h3" className="text-center text-foreground">
                Create your marketplace account
              </Text>
              <Text variant="muted" className="text-center">
                Join the Adelaide Uni community to buy, sell, and connect with peers.
              </Text>
            </View>
          </View>

          <View className="gap-5">
            <View className="gap-4">
              <Input
                value={fullName}
                onChangeText={setFullName}
                placeholder="Full name"
                className="h-14 rounded-2xl"
              />
              <Input
                value={email}
                onChangeText={setEmail}
                placeholder="University email"
                keyboardType="email-address"
                autoCapitalize="none"
                className="h-14 rounded-2xl"
              />
              <Input
                value={password}
                onChangeText={setPassword}
                placeholder="Password"
                secureTextEntry
                autoCapitalize="none"
                className="h-14 rounded-2xl"
              />
              <Input
                value={confirmPassword}
                onChangeText={setConfirmPassword}
                placeholder="Confirm password"
                secureTextEntry
                autoCapitalize="none"
                className="h-14 rounded-2xl"
              />
            </View>

            <Button className="h-14 rounded-2xl" onPress={() => router.push('/(tabs)/home')}>
              <Text className="text-base font-semibold text-primary-foreground">Create account</Text>
            </Button>

            <Link href="/" asChild>
              <Button variant="ghost">
                <Text className="text-sm text-muted-foreground">
                  Already have an account?{' '}
                  <Text className="text-sm font-semibold text-primary">Sign in</Text>
                </Text>
              </Button>
            </Link>
          </View>

          <View className="mx-auto mt-8 h-36 w-full max-w-sm items-center justify-center overflow-hidden rounded-3xl bg-accent/30">
            <View className="absolute -left-6 top-6 h-20 w-20 rounded-full bg-primary/20" />
            <View className="absolute -right-8 bottom-4 h-24 w-24 rounded-full bg-primary/30" />
            <Text className="text-sm font-medium text-primary">You're in good company</Text>
          </View>
        </View>
      </SafeAreaView>
    </>
  );
}
