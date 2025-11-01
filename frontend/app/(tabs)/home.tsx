import { Avatar } from '@/components/ui/avatar';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardFooter, CardTitle } from '@/components/ui/card';
import { Chip } from '@/components/ui/chip';
import { Icon } from '@/components/ui/icon';
import { Input } from '@/components/ui/input';
import { Text } from '@/components/ui/text';
import { router } from 'expo-router';
import * as React from 'react';
import { Image, ScrollView, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { BookmarkIcon, SearchIcon } from 'lucide-react-native';

const CATEGORIES = ['All', 'Books', 'Furniture', 'Tech', 'Clothing', 'Tickets', 'Services'];

const ITEMS = [
  {
    id: '1',
    title: 'Linear Algebra Textbook',
    price: 35,
    category: 'Books',
    seller: 'Alex Chen',
    image: 'https://images.unsplash.com/photo-1512820790803-83ca734da794?auto=format&fit=crop&w=900&q=80',
  },
  {
    id: '2',
    title: 'Vintage Timber Desk',
    price: 120,
    category: 'Furniture',
    seller: 'Jordan Smith',
    image: 'https://images.unsplash.com/photo-1519710164239-da123dc03ef4?auto=format&fit=crop&w=900&q=80',
  },
  {
    id: '3',
    title: 'iPad Air + Pencil',
    price: 480,
    category: 'Tech',
    seller: 'Priya Patel',
    image: 'https://images.unsplash.com/photo-1517336714731-489689fd1ca8?auto=format&fit=crop&w=900&q=80',
  },
  {
    id: '4',
    title: 'Graduation Gown Rental',
    price: 60,
    category: 'Services',
    seller: 'Ella Thompson',
    image: 'https://images.unsplash.com/photo-1523580846011-d3a5bc25702b?auto=format&fit=crop&w=900&q=80',
  },
];

export default function HomeScreen() {
  const [category, setCategory] = React.useState('All');

  const listings = React.useMemo(() => {
    if (category === 'All') {
      return ITEMS;
    }
    return ITEMS.filter((item) => item.category === category);
  }, [category]);

  return (
    <SafeAreaView className="flex-1 bg-background">
      <ScrollView contentContainerStyle={{ paddingBottom: 120 }} className="px-6 pt-4">
        <View className="mb-6 flex-row items-center justify-between">
          <View className="gap-2">
            <Text variant="muted">Welcome back</Text>
            <Text variant="h3" className="text-left">
              Marketplace
            </Text>
          </View>
          <Avatar name="Ava Student" />
        </View>

        <View className="gap-6">
          <View className="relative">
            <Input
              placeholder="Search items, books, electronics…"
              className="h-14 rounded-2xl pl-12"
            />
            <Icon as={SearchIcon} className="absolute left-4 top-4 text-muted-foreground" size={20} />
          </View>

          <ScrollView
            horizontal
            showsHorizontalScrollIndicator={false}
            contentContainerStyle={{ gap: 12 }}
            className="-ml-2"
            style={{ paddingHorizontal: 8 }}>
            {CATEGORIES.map((item) => (
              <Chip
                key={item}
                active={item === category}
                onPress={() => setCategory(item)}
                className="px-5 py-2">
                {item}
              </Chip>
            ))}
          </ScrollView>

          <View className="gap-5">
            {listings.map((item) => (
              <Card key={item.id} className="overflow-hidden">
                <View className="h-44 w-full overflow-hidden bg-muted">
                  <Image source={{ uri: item.image }} className="h-full w-full" resizeMode="cover" />
                </View>
                <CardContent>
                  <View className="flex-row items-center justify-between">
                    <Text className="text-sm font-medium text-primary/80">{item.category}</Text>
                    <View className="rounded-full bg-primary/10 px-3 py-1">
                      <Text className="text-xs font-semibold text-primary">${item.price}</Text>
                    </View>
                  </View>
                  <CardTitle>{item.title}</CardTitle>
                  <View className="flex-row items-center gap-3">
                    <Avatar name={item.seller} size={40} />
                    <Text variant="muted">Seller • {item.seller}</Text>
                  </View>
                </CardContent>
                <CardFooter>
                  <Button
                    variant="secondary"
                    className="rounded-2xl px-6"
                    onPress={() => router.push({ pathname: '/product/[id]', params: { id: item.id } })}>
                    <Text className="text-base font-semibold text-primary">View details</Text>
                  </Button>
                  <Button variant="ghost" size="icon" className="rounded-full bg-secondary">
                    <Icon as={BookmarkIcon} className="text-primary" size={20} />
                  </Button>
                </CardFooter>
              </Card>
            ))}
          </View>
        </View>
      </ScrollView>
    </SafeAreaView>
  );
}
