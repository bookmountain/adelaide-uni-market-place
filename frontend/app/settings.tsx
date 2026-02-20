import { Button } from '@/components/ui/button';
import { Icon } from '@/components/ui/icon';
import { Text } from '@/components/ui/text';
import { Stack, router } from 'expo-router';
import * as React from 'react';
import { ScrollView, Switch, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import {
  BellIcon,
  ChevronRightIcon,
  CreditCardIcon,
  HelpCircleIcon,
  LogOutIcon,
  ShieldIcon,
  UserRoundIcon,
} from 'lucide-react-native';

const SETTINGS_ITEMS = [
  { id: 'profile', label: 'Profile', icon: UserRoundIcon, action: () => {} },
  { id: 'notifications', label: 'Notifications', icon: BellIcon, toggle: true },
  { id: 'payment', label: 'Payment', icon: CreditCardIcon, action: () => {} },
  { id: 'security', label: 'Privacy & Security', icon: ShieldIcon, action: () => {} },
  { id: 'help', label: 'Help & Support', icon: HelpCircleIcon, action: () => {} },
];

export default function SettingsScreen() {
  const [notificationsEnabled, setNotificationsEnabled] = React.useState(true);

  const handleLogout = () => {
    // Navigate back to login screen and clear navigation stack
    router.replace('/');
  };

  return (
    <>
      <Stack.Screen options={{ title: 'Settings', headerShown: false }} />
      <SafeAreaView className="flex-1 bg-background">
        <ScrollView contentContainerStyle={{ paddingBottom: 120 }} className="px-6 pt-4">
          <View className="mb-6 gap-1">
            <Text variant="h3" className="text-left">
              Settings
            </Text>
            <Text variant="muted">Manage your preferences and account controls.</Text>
          </View>

          <View className="overflow-hidden rounded-3xl border border-border bg-card">
            {SETTINGS_ITEMS.map((item, index) => (
              <View
                key={item.id}
                className="flex-row items-center justify-between px-5 py-4"
                style={
                  index < SETTINGS_ITEMS.length - 1
                    ? { borderBottomWidth: 1, borderBottomColor: 'rgba(131, 125, 188, 0.12)' }
                    : undefined
                }>
                <View className="flex-row items-center gap-3">
                  <View className="h-10 w-10 items-center justify-center rounded-full bg-primary/10">
                    <Icon as={item.icon} className="text-primary" size={20} />
                  </View>
                  <Text className="text-base font-semibold text-foreground">{item.label}</Text>
                </View>
                {item.toggle ? (
                  <Switch
                    value={notificationsEnabled}
                    onValueChange={setNotificationsEnabled}
                    trackColor={{ false: 'rgba(171, 167, 204, 0.5)', true: '#B5A8FF' }}
                    thumbColor={notificationsEnabled ? '#836BFF' : '#f4f3f4'}
                  />
                ) : (
                  <Icon as={ChevronRightIcon} className="text-muted-foreground" size={18} />
                )}
              </View>
            ))}
          </View>

          <Button
            variant="ghost"
            className="mt-8 h-14 rounded-2xl border border-destructive/40"
            onPress={handleLogout}>
            <Icon as={LogOutIcon} className="text-destructive" size={20} />
            <Text className="text-base font-semibold text-destructive">Log out</Text>
          </Button>
        </ScrollView>
      </SafeAreaView>
    </>
  );
}
