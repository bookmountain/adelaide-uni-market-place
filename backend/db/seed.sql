-- Wipe existing seed data (idempotent)
DELETE FROM listing_images
WHERE "Id" IN (
    '30000000-0000-0000-0000-000000000001',
    '30000000-0000-0000-0000-000000000002',
    '30000000-0000-0000-0000-000000000003',
    '30000000-0000-0000-0000-000000000004'
);

DELETE FROM items
WHERE "Id" IN (
    '20000000-0000-0000-0000-000000000001',
    '20000000-0000-0000-0000-000000000002'
);

DELETE FROM users
WHERE "Email" = 'student@adelaide.edu.au';

DELETE FROM categories
WHERE "Slug" IN (
    'textbooks', 'electronics', 'furniture', 'stationery',
    'clothing', 'sports-recreation', 'transport',
    'events-tickets', 'miscellaneous'
);

-- Seed categories
INSERT INTO categories ("Id", "Name", "Slug")
VALUES
    ('00000000-0000-0000-0000-000000000101', 'Textbooks', 'textbooks'),
    ('00000000-0000-0000-0000-000000000102', 'Electronics', 'electronics'),
    ('00000000-0000-0000-0000-000000000103', 'Furniture', 'furniture'),
    ('00000000-0000-0000-0000-000000000104', 'Stationery', 'stationery'),
    ('00000000-0000-0000-0000-000000000105', 'Clothing', 'clothing'),
    ('00000000-0000-0000-0000-000000000106', 'Sports & Recreation', 'sports-recreation'),
    ('00000000-0000-0000-0000-000000000107', 'Transport', 'transport'),
    ('00000000-0000-0000-0000-000000000108', 'Events & Tickets', 'events-tickets'),
    ('00000000-0000-0000-0000-000000000109', 'Miscellaneous', 'miscellaneous')
;

-- Seed demo student user
INSERT INTO users (
    "Id",
    "Email",
    "DisplayName",
    "CreatedAt",
    "Role",
    "PasswordHash",
    "Department",
    "Degree",
    "Sex",
    "AvatarUrl",
    "Nationality",
    "Age",
    "IsActive",
    "ActivationToken",
    "ActivationTokenExpiresAt"
)
VALUES (
    '11111111-2222-3333-4444-555555555555',
    'student@adelaide.edu.au',
    'Seed Student',
    NOW(),
    'Student',
    '$2a$11$.bgaTMiXjFxfZvHMAKcq3OQYEsoK6jhXqnCgpDwubTSD1c9uAYKyC',
    'ComputerScience',
    'Bachelor',
    'PreferNotToSay',
    'https://images.unsplash.com/photo-1527980965255-d3b416303d12?w=640',
    'Australia',
    21,
    TRUE,
    NULL,
    NULL
)
;

-- Seed textbook sample item
INSERT INTO items (
    "Id",
    "SellerId",
    "CategoryId",
    "Title",
    "Description",
    "Price",
    "Status",
    "CreatedAt",
    "UpdatedAt"
)
VALUES (
    '20000000-0000-0000-0000-000000000001',
    '11111111-2222-3333-4444-555555555555',
    '00000000-0000-0000-0000-000000000101',
    'Calculus II Textbook',
    'Lightly highlighted second-year calculus book in great condition.',
    45.00,
    1,
    NOW() - INTERVAL '7 days',
    NOW() - INTERVAL '7 days'
)
;

INSERT INTO listing_images ("Id", "ItemId", "Url", "SortOrder")
VALUES
    ('30000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000001', 'https://images.unsplash.com/photo-1524995997946-a1c2e315a42f?w=800', 1),
    ('30000000-0000-0000-0000-000000000002', '20000000-0000-0000-0000-000000000001', 'https://images.unsplash.com/photo-1522204538344-d26c3a3b271e?w=800', 2)
;

-- Seed laptop sample item
INSERT INTO items (
    "Id",
    "SellerId",
    "CategoryId",
    "Title",
    "Description",
    "Price",
    "Status",
    "CreatedAt",
    "UpdatedAt"
)
VALUES (
    '20000000-0000-0000-0000-000000000002',
    '11111111-2222-3333-4444-555555555555',
    '00000000-0000-0000-0000-000000000102',
    '13" Ultrabook Laptop',
    'Lightweight laptop, perfect for lectures. Includes charger and sleeve.',
    620.00,
    1,
    NOW() - INTERVAL '3 days',
    NOW() - INTERVAL '3 days'
)
;

INSERT INTO listing_images ("Id", "ItemId", "Url", "SortOrder")
VALUES
    ('30000000-0000-0000-0000-000000000003', '20000000-0000-0000-0000-000000000002', 'https://images.unsplash.com/photo-1517336714731-489689fd1ca8?w=800', 1),
    ('30000000-0000-0000-0000-000000000004', '20000000-0000-0000-0000-000000000002', 'https://images.unsplash.com/photo-1517245386807-bb43f82c33c4?w=800', 2)
;
