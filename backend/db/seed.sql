-- Example seed data for PostgreSQL
INSERT INTO categories (id, name, slug)
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
ON CONFLICT (id) DO NOTHING;
