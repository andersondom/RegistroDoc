using Microsoft.EntityFrameworkCore;
using RegistroDoc.Domain.Entities;

namespace RegistroDoc.Infrastructure.Persistence;

public class RegistroDocDbContext : DbContext
{
    public RegistroDocDbContext(DbContextOptions<RegistroDocDbContext> options)
        : base(options)
    {
    }

    public DbSet<Serventia> Serventias => Set<Serventia>();

    public DbSet<Documento> Documentos => Set<Documento>();

    public DbSet<PaginaDocumento> PaginasDocumento => Set<PaginaDocumento>();

    public DbSet<ExecucaoIndexacao> ExecucoesIndexacao => Set<ExecucaoIndexacao>();

    public DbSet<Auditoria> Auditorias => Set<Auditoria>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Serventia>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Nome)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.CodigoCns)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(x => x.Municipio)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(x => x.Uf)
                .IsRequired()
                .HasMaxLength(2);

            entity.HasIndex(x => x.CodigoCns)
                .IsUnique();
        });

        modelBuilder.Entity<Documento>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.TipoDocumento)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.NomeArquivo)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(x => x.CaminhoRelativo)
                .IsRequired()
                .HasMaxLength(2000);

            entity.Property(x => x.HashSha256)
                .IsRequired()
                .HasMaxLength(64);

            entity.Property(x => x.StatusIndexacao)
                .IsRequired()
                .HasMaxLength(30);

            entity.HasOne(x => x.Serventia)
                .WithMany()
                .HasForeignKey(x => x.ServentiaId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.ServentiaId);

            entity.HasIndex(x => x.StatusIndexacao);

            entity.HasIndex(x => x.HashSha256)
                .IsUnique();
        });

        modelBuilder.Entity<PaginaDocumento>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.TextoExtraido)
                .IsRequired();

            entity.HasOne(x => x.Documento)
                .WithMany()
                .HasForeignKey(x => x.DocumentoId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => new
            {
                x.DocumentoId,
                x.NumeroPagina
            }).IsUnique();
        });

        modelBuilder.Entity<ExecucaoIndexacao>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Status)
                .IsRequired()
                .HasMaxLength(30);

            entity.HasOne(x => x.Documento)
                .WithMany()
                .HasForeignKey(x => x.DocumentoId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => x.DocumentoId);
        });

        modelBuilder.Entity<Auditoria>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Entidade)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(x => x.EntidadeId)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(x => x.Acao)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.UsuarioId)
                .HasMaxLength(100);

            entity.HasIndex(x => x.OcorridaEmUtc);
        });
    }
}

