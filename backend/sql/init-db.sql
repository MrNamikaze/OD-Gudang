CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE TABLE IF NOT EXISTS users (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    username VARCHAR(50) NOT NULL UNIQUE,
    email VARCHAR(150) NOT NULL UNIQUE,
    full_name VARCHAR(150) NOT NULL,
    password_hash TEXT NOT NULL,
    role VARCHAR(20) NOT NULL CHECK (role IN ('Admin', 'User')),
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

INSERT INTO users (username, email, full_name, password_hash, role)
SELECT
    'admin',
    'admin@od-gudang.local',
    'Warehouse Administrator',
    crypt('Admin!23456', gen_salt('bf', 12)),
    'Admin'
WHERE NOT EXISTS (
    SELECT 1 FROM users WHERE username = 'admin'
);

INSERT INTO users (username, email, full_name, password_hash, role)
SELECT
    'operator',
    'operator@od-gudang.local',
    'Warehouse Operator',
    crypt('User!23456', gen_salt('bf', 12)),
    'User'
WHERE NOT EXISTS (
    SELECT 1 FROM users WHERE username = 'operator'
);

CREATE TABLE IF NOT EXISTS detection_records (
    id UUID PRIMARY KEY,
    timestamp_utc TIMESTAMPTZ NOT NULL,
    object_name VARCHAR(120) NOT NULL,
    camera_id VARCHAR(80) NOT NULL,
    image_path VARCHAR(500) NOT NULL,
    confidence NUMERIC(10,4) NOT NULL,
    zone VARCHAR(120) NOT NULL,
    created_at_utc TIMESTAMPTZ NOT NULL
);
