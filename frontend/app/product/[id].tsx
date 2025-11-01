import { Avatar } from '@/components/ui/avatar';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardTitle } from '@/components/ui/card';
import { Text } from '@/components/ui/text';
import { Stack, useLocalSearchParams } from 'expo-router';
import * as React from 'react';
import { Image, ScrollView, View } from 'react-native';
import { SafeAreaView, useSafeAreaInsets } from 'react-native-safe-area-context';

const PRODUCTS = [
  {
    id: '1',
    title: 'Linear Algebra Textbook',
    price: 35,
    seller: 'Alex Chen',
    sellerRole: 'Fourth-year Engineering',
    description:
      'Lightly highlighted, includes exercise solutions. Perfect for MATHS 2102. Pick-up on North Terrace campus or postage at buyer’s cost.',
    images: [
      'https://images.unsplash.com/photo-1529148482759-b35b25c5f217?auto=format&fit=crop&w=1000&q=80',
      'https://images.unsplash.com/photo-1463320726281-696a485928c7?auto=format&fit=crop&w=1000&q=80',
      'https://images.unsplash.com/photo-1522202176988-66273c2fd55f?auto=format&fit=crop&w=1000&q=80',
    ],
  },
  {
    id: '2',
    title: 'Vintage Timber Desk',
    price: 120,
    seller: 'Jordan Smith',
    sellerRole: 'Architecture Alumni',
    description:
      'Solid timber desk with ample storage. Perfect for studio projects. Includes desk lamp and cable tray.',
    images: [
      'https://images.unsplash.com/photo-1524758631624-e2822e304c36?auto=format&fit=crop&w=1000&q=80',
      'https://images.unsplash.com/photo-1505693416388-ac5ce068fe85?auto=format&fit=crop&w=1000&q=80',
    ],
  },
  {
    id: '3',
    title: 'iPad Air + Pencil',
    price: 480,
    seller: 'Priya Patel',
    sellerRole: 'Design Honours',
    description:
      'iPad Air (2022) with Apple Pencil, perfect for note-taking and Procreate. Battery at 93%. Includes Smart Folio case.',
    images: [
      'https://images.unsplash.com/photo-1555967522-37949fc21dcb?auto=format&fit=crop&w=1000&q=80',
      'https://images.unsplash.com/photo-1545239351-1141bd82e8a6?auto=format&fit=crop&w=1000&q=80',
    ],
  },
];

export default function ProductDetailScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const insets = useSafeAreaInsets();

  const product = React.useMemo(() => {
    return PRODUCTS.find((item) => item.id === id) ?? PRODUCTS[0];
  }, [id]);

  return (
    <>
      <Stack.Screen options={{ headerShown: false }} />
      <SafeAreaView className="flex-1 bg-background">
      <View className="flex-1">
        <ScrollView contentContainerStyle={{ paddingBottom: 160 }}>
          <ScrollView horizontal pagingEnabled showsHorizontalScrollIndicator={false}>
            {product.images.map((uri, index) => (
              <Image
                key={index}
                source={{ uri }}
                style={{ width: '100%', height: 320 }}
                resizeMode="cover"
              />
            ))}
          </ScrollView>

          <View className="px-6 py-6">
            <Card>
              <CardContent className="gap-5">
                <View className="flex-row items-center justify-between">
                  <View>
                    <Text className="text-sm font-medium text-primary/80">Campus listing</Text>
                    <CardTitle>{product.title}</CardTitle>
                  </View>
                  <View className="rounded-2xl bg-primary/10 px-4 py-2">
                    <Text className="text-lg font-semibold text-primary">${product.price}</Text>
                  </View>
                </View>

                <View className="flex-row items-center gap-3">
                  <Avatar name={product.seller} size={48} />
                  <View>
                    <Text className="text-base font-semibold text-foreground">{product.seller}</Text>
                    <CardDescription>{product.sellerRole}</CardDescription>
                  </View>
                </View>

                <View className="gap-3">
                  <Text className="text-sm font-semibold text-foreground">Description</Text>
                  <CardDescription>{product.description}</CardDescription>
                </View>
              </CardContent>
            </Card>
          </View>
        </ScrollView>

        <View
          style={{
            paddingBottom: Math.max(insets.bottom, 24),
          }}
          className="border-t border-border bg-card px-6 py-4">
          <View className="flex-row items-center gap-3">
            <Button className="flex-1 h-14 rounded-2xl bg-primary">
              <Text className="text-base font-semibold text-primary-foreground">Buy now</Text>
            </Button>
            <Button variant="outline" className="flex-1 h-14 rounded-2xl">
              <Text className="text-base font-semibold text-primary">Chat with seller</Text>
            </Button>
          </View>
        </View>
      </View>
    </SafeAreaView>
    </>
  );
}
