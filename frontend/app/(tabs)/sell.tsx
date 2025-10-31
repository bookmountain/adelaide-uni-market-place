import { Button } from '@/components/ui/button';
import { Card, CardContent, CardTitle } from '@/components/ui/card';
import { Chip } from '@/components/ui/chip';
import { Icon } from '@/components/ui/icon';
import { Input } from '@/components/ui/input';
import { Text } from '@/components/ui/text';
import * as React from 'react';
import { Pressable, ScrollView, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { CameraIcon, UploadIcon } from 'lucide-react-native';

const CATEGORIES = ['Books', 'Furniture', 'Tech', 'Clothing', 'Tickets', 'Services'];

export default function ListingFormScreen() {
  const [category, setCategory] = React.useState<string | null>('Books');

  return (
    <SafeAreaView className="flex-1 bg-background">
      <ScrollView contentContainerStyle={{ paddingBottom: 140 }} className="px-6 pt-4">
        <View className="mb-6 gap-2">
          <Text variant="muted">Create listing</Text>
          <Text variant="h3" className="text-left">
            Post a new item
          </Text>
        </View>

        <Card>
          <CardContent className="gap-6">
            <View className="gap-2">
              <Text variant="small" className="text-muted-foreground">
                Title
              </Text>
              <Input placeholder="e.g. Acoustic Guitar with Case" className="h-14 rounded-2xl" />
            </View>

            <View className="gap-2">
              <Text variant="small" className="text-muted-foreground">
                Category
              </Text>
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
            </View>

            <View className="gap-2">
              <Text variant="small" className="text-muted-foreground">
                Price (AUD)
              </Text>
              <Input placeholder="75.00" keyboardType="numeric" className="h-14 rounded-2xl" />
            </View>

            <View className="gap-2">
              <Text variant="small" className="text-muted-foreground">
                Description
              </Text>
              <Input
                multiline
                numberOfLines={4}
                textAlignVertical="top"
                placeholder="Share details that help buyers decide faster…"
                className="h-auto min-h-[120px] rounded-2xl px-4 py-4"
              />
            </View>

            <View className="gap-3">
              <View className="flex-row items-center justify-between">
                <Text variant="small" className="text-muted-foreground">
                  Photos
                </Text>
                <Button variant="ghost" className="gap-2">
                  <Icon as={UploadIcon} className="text-primary" size={18} />
                  <Text className="text-sm font-semibold text-primary">Upload</Text>
                </Button>
              </View>

              <View className="flex-row flex-wrap gap-3">
                {Array.from({ length: 3 }).map((_, index) => (
                  <Pressable
                    key={index}
                    className="h-24 w-24 items-center justify-center rounded-2xl border border-dashed border-primary/60 bg-primary/5">
                    <Icon as={CameraIcon} className="text-primary" size={22} />
                    <Text className="mt-1 text-xs text-muted-foreground">Add photo</Text>
                  </Pressable>
                ))}
              </View>

              <View className="gap-1">
                <Text className="text-xs font-medium text-muted-foreground">Uploading 2 / 5</Text>
                <View className="h-2 rounded-full bg-secondary">
                  <View className="h-full w-2/3 rounded-full bg-primary" />
                </View>
              </View>
            </View>
          </CardContent>
        </Card>

        <View className="mt-8 gap-3">
          <Button className="h-14 rounded-2xl">
            <Text className="text-base font-semibold text-primary-foreground">Post item</Text>
          </Button>
          <Button variant="outline" className="h-14 rounded-2xl border-dashed border-primary/40">
            <Text className="text-sm font-semibold text-primary">Save draft</Text>
          </Button>
        </View>
      </ScrollView>
    </SafeAreaView>
  );
}
