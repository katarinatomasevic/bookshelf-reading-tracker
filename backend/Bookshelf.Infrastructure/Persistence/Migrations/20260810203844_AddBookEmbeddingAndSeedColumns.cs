using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace Bookshelf.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBookEmbeddingAndSeedColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.AddColumn<Vector>(
                name: "Embedding",
                table: "Book",
                type: "vector(384)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PopularityRank",
                table: "Book",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SeedTopic",
                table: "Book",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Book_Embedding",
                table: "Book",
                column: "Embedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Book_Embedding",
                table: "Book");

            migrationBuilder.DropColumn(
                name: "Embedding",
                table: "Book");

            migrationBuilder.DropColumn(
                name: "PopularityRank",
                table: "Book");

            migrationBuilder.DropColumn(
                name: "SeedTopic",
                table: "Book");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");
        }
    }
}
