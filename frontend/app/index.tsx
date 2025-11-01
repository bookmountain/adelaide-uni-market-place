import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Text } from '@/components/ui/text';
import { Link, Stack, router } from 'expo-router';
import * as React from 'react';
import { SafeAreaView, View } from 'react-native';

export default function LoginScreen() {
  const [email, setEmail] = React.useState('');
  const [password, setPassword] = React.useState('');

  return (
    <>
      <Stack.Screen options={{ headerShown: false }} />
      <SafeAreaView className="flex-1 bg-background">
        <View className="flex-1 justify-between px-6 py-10">
          <View className="items-center gap-8">
            <View className="h-16 w-16 items-center justify-center rounded-2xl bg-primary/10">
              <Text className="text-2xl font-semibold text-primary">AUM</Text>
            </View>
            <View className="gap-3">
              <Text variant="h3" className="text-center text-foreground">
                Sign in to Adelaide Uni Marketplace
              </Text>
              <Text variant="muted" className="text-center">
                Discover trusted listings from fellow students and alumni.｀
              </Text>
            </View>
          </View>

          <View className="gap-6">
            <View className="gap-4">
              <View className="gap-2">
                <Text variant="small" className="text-muted-foreground">
                  Email
                </Text>
                <Input
                  value={email}
                  onChangeText={setEmail}
                  placeholder="you@adelaide.edu.au"
                  keyboardType="email-address"
                  autoCapitalize="none"
                  autoCorrect={false}
                  className="h-14 rounded-2xl"
                />
              </View>
              <View className="gap-2">
                <Text variant="small" className="text-muted-foreground">
                  Password
                </Text>
                <Input
                  value={password}
                  onChangeText={setPassword}
                  placeholder="Enter your password"
                  secureTextEntry
                  autoCapitalize="none"
                  className="h-14 rounded-2xl"
                />
              </View>
            </View>
            <Button className="h-14 rounded-2xl" onPress={() => router.push('/(tabs)/home')}>
              <Text className="text-base font-semibold text-primary-foreground">Continue</Text>
            </Button>
            <Link href="/register" asChild>
              <Button variant="ghost">
                <Text className="text-sm text-muted-foreground">
                  New here?{' '}
                  <Text className="text-sm font-semibold text-primary">Create an account</Text>
                </Text>
              </Button>
            </Link>
          </View>
        </View>
      </SafeAreaView>
    </>
  );
}
