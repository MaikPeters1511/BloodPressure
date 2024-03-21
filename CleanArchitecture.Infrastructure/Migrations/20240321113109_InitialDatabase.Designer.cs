﻿// <auto-generated />
using System;
using CleanArchitecture.Infrastructure.Persistence.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CleanArchitecture.Infrastructure.Migrations
{
    [DbContext(typeof(AddPostgresSqlDbContext))]
    [Migration("20240321113109_InitialDatabase")]
    partial class InitialDatabase
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.3")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("CleanArchitecture.Domain.Entities.BloodPressure", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<DateTime>("DateTime")
                        .HasColumnType("timestamp with time zone");

                    b.Property<int>("Diastolisch")
                        .HasMaxLength(3)
                        .HasColumnType("integer");

                    b.Property<int>("Oxygen")
                        .HasMaxLength(3)
                        .HasColumnType("integer");

                    b.Property<int>("Pulse")
                        .HasMaxLength(3)
                        .HasColumnType("integer");

                    b.Property<int>("Systolisch")
                        .HasMaxLength(3)
                        .HasColumnType("integer");

                    b.HasKey("Id");

                    b.ToTable("BloodPressure", (string)null);
                });
#pragma warning restore 612, 618
        }
    }
}
