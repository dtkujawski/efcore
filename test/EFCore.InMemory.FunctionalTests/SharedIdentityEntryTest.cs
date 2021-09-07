// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;

// ReSharper disable InconsistentNaming
namespace Microsoft.EntityFrameworkCore
{

    public class SharedIdentityEntryTest : IClassFixture<InMemoryFixture>
    {
        public SharedIdentityEntryTest(InMemoryFixture fixture)
            => Fixture = fixture;

        protected InMemoryFixture Fixture { get; }

        /// <summary>When deleting and adding a record with the same key, EF inMemory needs to delete the record before inserting it in InMemoryStore</summary>
        [ConditionalFact]
        public virtual void Shared_identity_entry_delete_and_insert_with_concurrency()
        {
            using (CreateScratch<DatabaseContext>(Seed, nameof(SharedIdentityEntryTest)))
            using (var context = new DatabaseContext())
            {
                //mark existing record as "deleted" in change tracker
                context.Records.RemoveRange(context.Records);

                //insert new record with same key as being deleted
                context.Add(new Record { Key = "KEY", Concurrency = new DateTime(2021, 2, 2, 2, 2, 2) });

                //save (should delete 1st record and add second record)
                context.SaveChanges();

                var record = context.Records.FirstOrDefault();
                Assert.Equal(1, context.Records.Count()); //only one record should exist
                Assert.NotNull(record); //record should be returned from fetch
                Assert.Equal(new DateTime(2021, 2, 2, 2, 2, 2), record.Concurrency); //record should not have concurrency of first entry
            }
        }

        private static void Seed(DatabaseContext context)
        {
            context.Add(new Record { Key = "KEY", Concurrency = new DateTime(2021, 1, 1, 1, 1, 1) });
            context.SaveChanges();
        }

        private class DatabaseContext : DbContext
        {
            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
                optionsBuilder
                    .UseInternalServiceProvider(InMemoryFixture.DefaultServiceProvider)
                    .UseInMemoryDatabase(nameof(SharedIdentityEntryTest));
            }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                _ = modelBuilder.Entity<Record>();
            }

            public DbSet<Record> Records { get; set; }
        }

        private class Record
        {
            [Key]
            public string Key { get; set; }
            [ConcurrencyCheck]
            public DateTime Concurrency { get; set; }
        }

        private static InMemoryTestStore CreateScratch<TContext>(Action<TContext> seed, string databaseName) where TContext : DbContext, new()
        {
            return InMemoryTestStore.GetOrCreate(databaseName).InitializeInMemory(null, () => new TContext(), c => seed((TContext)c));
        }

    }
}
