#!/bin/bash
set -e

echo "🔄 Starting database migration..."

# Wait for database
until pg_isready -h database -p 5432 -U postgres; do
    echo "Database is not ready, waiting 5 seconds..."
    sleep 5
done

echo "✅ Database is ready!"

# Run Entity Framework migrations
echo "🚀 Running Entity Framework migrations..."
dotnet CRM.dll --migrate

echo "✅ Migration completed successfully!"
sleep 10