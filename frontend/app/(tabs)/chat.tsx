import { Avatar } from '@/components/ui/avatar';
import { Button } from '@/components/ui/button';
import { Icon } from '@/components/ui/icon';
import { Input } from '@/components/ui/input';
import { Text } from '@/components/ui/text';
import * as React from 'react';
import { KeyboardAvoidingView, Platform, ScrollView, View } from 'react-native';
import { SafeAreaView, useSafeAreaInsets } from 'react-native-safe-area-context';
import { ArrowLeftIcon, ImageIcon, SendIcon, SmileIcon } from 'lucide-react-native';

const MESSAGES = [
  { id: '1', sender: 'them', text: 'Hi! Is the iPad bundle still available?', time: '09:12' },
  { id: '2', sender: 'me', text: 'Hi Sophie! Yes, it’s available and in great condition.', time: '09:14' },
  { id: '3', sender: 'them', text: 'Awesome! Could we meet on campus this Thursday?', time: '09:16' },
  {
    id: '4',
    sender: 'me',
    text: 'Thursday works. Library atrium at 2pm?',
    time: '09:18',
  },
  {
    id: '5',
    sender: 'them',
    text: 'Perfect, see you then! I’ll bring cash.',
    time: '09:20',
  },
];

export default function ChatScreen() {
  const insets = useSafeAreaInsets();
  const [message, setMessage] = React.useState('');

  return (
    <SafeAreaView className="flex-1 bg-background" edges={['top']}>
      <KeyboardAvoidingView
        behavior={Platform.OS === 'ios' ? 'padding' : undefined}
        style={{ flex: 1 }}>
        <View className="flex-1 justify-between">
          <View className="border-b border-border px-6 pb-4 pt-2">
            <View className="flex-row items-center gap-3">
              <Button size="icon" variant="ghost" className="rounded-full bg-secondary">
                <Icon as={ArrowLeftIcon} className="text-primary" size={20} />
              </Button>
              <Avatar name="Sophie Turner" size={48} />
              <View className="flex-1">
                <Text className="text-base font-semibold text-foreground">Sophie Turner</Text>
                <Text variant="muted">Last active 2h ago</Text>
              </View>
            </View>
          </View>

          <ScrollView
            contentContainerStyle={{ paddingHorizontal: 24, paddingVertical: 24 }}
            className="flex-1">
            <View className="gap-4">
              {MESSAGES.map((item) => (
                <View key={item.id} className={item.sender === 'me' ? 'items-end' : 'items-start'}>
                  <View
                    className={
                      item.sender === 'me'
                        ? 'max-w-[80%] rounded-3xl rounded-br-sm bg-primary px-5 py-3'
                        : 'max-w-[80%] rounded-3xl rounded-bl-sm bg-[#F1F0FF] px-5 py-3'
                    }>
                    <Text
                      className={
                        item.sender === 'me'
                          ? 'text-base text-primary-foreground'
                          : 'text-base text-foreground'
                      }>
                      {item.text}
                    </Text>
                  </View>
                  <Text variant="muted" className="mt-1 text-xs">
                    {item.time}
                  </Text>
                </View>
              ))}
            </View>
          </ScrollView>

          <View
            style={{ paddingBottom: Math.max(insets.bottom, 16) }}
            className="border-t border-border bg-card px-4 py-4">
            <View className="flex-row items-center gap-3 rounded-2xl bg-secondary px-3 py-2">
              <Button size="icon" variant="ghost" className="rounded-full bg-card">
                <Icon as={ImageIcon} className="text-primary" size={20} />
              </Button>
              <Button size="icon" variant="ghost" className="rounded-full bg-card">
                <Icon as={SmileIcon} className="text-primary" size={20} />
              </Button>
              <Input
                value={message}
                onChangeText={setMessage}
                placeholder="Write a message…"
                className="flex-1 border-none bg-transparent px-0"
              />
              <Button size="icon" variant="default" className="rounded-full bg-primary">
                <Icon as={SendIcon} className="text-primary-foreground" size={18} />
              </Button>
            </View>
          </View>
        </View>
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
}
