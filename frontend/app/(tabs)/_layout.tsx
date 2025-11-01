import { NAV_THEME } from '@/lib/theme';
import { Tabs } from 'expo-router';
import { useColorScheme } from 'nativewind';
import {
  HomeIcon,
  MessageCircleIcon,
  PlusCircleIcon,
  UserRoundIcon,
} from 'lucide-react-native';

export default function TabsLayout() {
  const { colorScheme } = useColorScheme();

  return (
    <Tabs
      screenOptions={{
        headerShown: false,
        tabBarActiveTintColor: NAV_THEME[colorScheme ?? 'light'].colors.primary,
        tabBarInactiveTintColor: 'rgba(130, 130, 155, 0.7)',
        tabBarLabelStyle: { fontSize: 12, fontWeight: '600' },
        tabBarStyle: {
          borderTopColor: 'transparent',
          backgroundColor: 'rgba(255,255,255,0.95)',
          position: 'absolute',
          marginHorizontal: 16,
          marginBottom: 20,
          borderRadius: 24,
          height: 70,
          paddingBottom: 10,
          paddingTop: 10,
          shadowColor: '#1E1A3C',
          shadowOpacity: 0.1,
          shadowRadius: 20,
          shadowOffset: { width: 0, height: 12 },
          elevation: 6,
        },
      }}>
      <Tabs.Screen
        name="home"
        options={{
          title: 'Home',
          tabBarIcon: ({ color, size }) => <HomeIcon color={color} size={size} />,
        }}
      />
      <Tabs.Screen
        name="chat"
        options={{
          title: 'Chat',
          tabBarIcon: ({ color, size }) => <MessageCircleIcon color={color} size={size} />,
        }}
      />
      <Tabs.Screen
        name="sell"
        options={{
          title: 'Sell',
          tabBarIcon: ({ color, size }) => <PlusCircleIcon color={color} size={size} />,
        }}
      />
      <Tabs.Screen
        name="profile"
        options={{
          title: 'Profile',
          tabBarIcon: ({ color, size }) => <UserRoundIcon color={color} size={size} />,
        }}
      />
    </Tabs>
  );
}
