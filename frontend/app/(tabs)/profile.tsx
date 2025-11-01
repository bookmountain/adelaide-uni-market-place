import { Avatar } from '@/components/ui/avatar';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardTitle } from '@/components/ui/card';
import { Icon } from '@/components/ui/icon';
import { Text } from '@/components/ui/text';
import { router } from 'expo-router';
import * as React from 'react';
import { ScrollView, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import {
  BarChart3Icon,
  ChevronRightIcon,
  InboxIcon,
  PackagePlusIcon,
  SettingsIcon,
  ShoppingBagIcon,
} from 'lucide-react-native';

const STATS = [
  { label: 'Active listings', value: 12 },
  { label: 'Items sold', value: 48 },
  { label: 'Chats', value: 9 },
];

const QUICK_ACTIONS = [
  { label: 'Add item', icon: PackagePlusIcon, route: '/(tabs)/sell' as const },
  { label: 'Manage listings', icon: ShoppingBagIcon },
  { label: 'View orders', icon: BarChart3Icon },
];

const RECENT_MESSAGES = [
  {
    id: '1',
    name: 'Chris O’Neil',
    preview: 'Thanks for keeping the book aside!',
    time: '2m ago',
  },
  {
    id: '2',
    name: 'Amelia Bryant',
    preview: 'Can I pickup the bike tonight?',
    time: '1h ago',
  },
  {
    id: '3',
    name: 'Marcus Lim',
    preview: 'Payment sent, see you tomorrow.',
    time: 'Yesterday',
  },
];

export default function SellerDashboardScreen() {
  return (
    <SafeAreaView className="flex-1 bg-background">
      <ScrollView contentContainerStyle={{ paddingBottom: 140 }} className="px-6 pt-4">
        <View className="mb-6 flex-row items-center justify-between">
          <View className="gap-1">
            <Text variant="muted">Seller overview</Text>
            <Text variant="h3" className="text-left">
              Dashboard
            </Text>
          </View>
          <Avatar name="Taylor Swift" />
        </View>

        <Card>
          <CardContent className="gap-5">
            <View className="flex-row items-center justify-between">
              {STATS.map((stat) => (
                <View key={stat.label} className="items-start gap-1">
                  <Text className="text-2xl font-bold text-primary">{stat.value}</Text>
                  <Text variant="muted" className="text-xs uppercase tracking-wide">
                    {stat.label}
                  </Text>
                </View>
              ))}
            </View>
          </CardContent>
        </Card>

        <Card className="mt-6">
          <CardContent className="gap-4">
            <CardTitle>Quick actions</CardTitle>
            <View className="gap-3">
              {QUICK_ACTIONS.map((action) => (
                <Button
                  key={action.label}
                  variant="outline"
                  className="h-14 flex-row items-center justify-between rounded-2xl border-border px-4"
                  onPress={() => {
                    if (action.route) {
                      router.push(action.route);
                    }
                  }}>
                  <View className="flex-row items-center gap-3">
                    <View className="h-10 w-10 items-center justify-center rounded-full bg-primary/10">
                      <Icon as={action.icon} className="text-primary" size={20} />
                    </View>
                    <Text className="text-base font-semibold text-foreground">{action.label}</Text>
                  </View>
                  <Icon as={ChevronRightIcon} className="text-muted-foreground" size={18} />
                </Button>
              ))}
            </View>
          </CardContent>
        </Card>

        <Card className="mt-6">
          <CardContent className="gap-3">
            <View className="flex-row items-center justify-between">
              <CardTitle>Recent messages</CardTitle>
              <Button variant="ghost">
                <Icon as={InboxIcon} className="text-primary" size={18} />
                <Text className="text-sm font-semibold text-primary">View all</Text>
              </Button>
            </View>
            <View className="gap-4">
              {RECENT_MESSAGES.map((message) => (
                <View
                  key={message.id}
                  className="flex-row items-center gap-4 rounded-2xl border border-transparent bg-secondary px-4 py-3">
                  <Avatar name={message.name} size={40} />
                  <View>
                    <Text className="font-semibold text-foreground">{message.name}</Text>
                    <CardDescription>{message.preview}</CardDescription>
                    <Text variant="muted" className="shrink text-right text-xs">
                      {message.time}
                    </Text>
                  </View>
                </View>
              ))}
            </View>
          </CardContent>
        </Card>

        <Button
          className="mt-8 h-14 rounded-2xl"
          variant="secondary"
          onPress={() => router.push('/settings')}>
          <Icon as={SettingsIcon} className="text-primary" size={18} />
          <Text className="text-base font-semibold text-primary">Account & settings</Text>
        </Button>
      </ScrollView>
    </SafeAreaView>
  );
}
