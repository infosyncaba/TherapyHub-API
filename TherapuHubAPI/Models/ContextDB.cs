using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace TherapuHubAPI.Models;

public partial class ContextDB : DbContext
{
    public ContextDB()
    {
    }

    public ContextDB(DbContextOptions<ContextDB> options)
        : base(options)
    {
    }

    public virtual DbSet<ActorRelationships> ActorRelationships { get; set; }

    public virtual DbSet<Actors> Actors { get; set; }

    public virtual DbSet<ChatMessages> ChatMessages { get; set; }

    public virtual DbSet<ClientStatuses> ClientStatuses { get; set; }

    public virtual DbSet<Clients> Clients { get; set; }

    public virtual DbSet<Companies> Companies { get; set; }

    public virtual DbSet<CompanyChats> CompanyChats { get; set; }

    public virtual DbSet<EventTypes> EventTypes { get; set; }

    public virtual DbSet<EventUsers> EventUsers { get; set; }

    public virtual DbSet<Events> Events { get; set; }

    public virtual DbSet<Files> Files { get; set; }

    public virtual DbSet<FilesTypes> FilesTypes { get; set; }

    public virtual DbSet<FolderTypes> FolderTypes { get; set; }

    public virtual DbSet<Folders> Folders { get; set; }

    public virtual DbSet<GoalTrackerCategories> GoalTrackerCategories { get; set; }

    public virtual DbSet<GoalTrackerItems> GoalTrackerItems { get; set; }

    public virtual DbSet<GoalTrackerStatus> GoalTrackerStatus { get; set; }

    public virtual DbSet<GoalTrackers> GoalTrackers { get; set; }

    public virtual DbSet<JobTitles> JobTitles { get; set; }

    public virtual DbSet<LibraryCategories> LibraryCategories { get; set; }

    public virtual DbSet<LibraryItemFiles> LibraryItemFiles { get; set; }

    public virtual DbSet<LibraryItems> LibraryItems { get; set; }

    public virtual DbSet<Menus> Menus { get; set; }

    public virtual DbSet<MessageReads> MessageReads { get; set; }

    public virtual DbSet<NoteCategories> NoteCategories { get; set; }

    public virtual DbSet<NotePriorities> NotePriorities { get; set; }

    public virtual DbSet<NoteSections> NoteSections { get; set; }

    public virtual DbSet<NoteTypes> NoteTypes { get; set; }

    public virtual DbSet<Notes> Notes { get; set; }

    public virtual DbSet<RelationshipType> RelationshipType { get; set; }

    public virtual DbSet<SessionNotesStatus> SessionNotesStatus { get; set; }

    public virtual DbSet<SessionsNotes> SessionsNotes { get; set; }

    public virtual DbSet<Staff> Staff { get; set; }

    public virtual DbSet<StaffDocumentTypes> StaffDocumentTypes { get; set; }

    public virtual DbSet<StaffRoles> StaffRoles { get; set; }

    public virtual DbSet<StaffStatus> StaffStatus { get; set; }

    public virtual DbSet<StaffTimeOff> StaffTimeOff { get; set; }

    public virtual DbSet<StorageEntities> StorageEntities { get; set; }

    public virtual DbSet<TimeOffStatus> TimeOffStatus { get; set; }

    public virtual DbSet<TimeOffTypes> TimeOffTypes { get; set; }

    public virtual DbSet<UserDelegations> UserDelegations { get; set; }

    public virtual DbSet<UserTypeMenus> UserTypeMenus { get; set; }

    public virtual DbSet<UserTypes> UserTypes { get; set; }

    public virtual DbSet<Users> Users { get; set; }

    public virtual DbSet<library_permissions> library_permissions { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
            optionsBuilder.UseNpgsql("Host=dpg-d88fha0jo6nc73cr09mg-a.oregon-postgres.render.com;Database=therapyhub_db;Username=therapyhub_db_user;Password=W0cABdZ0FfFIFr9QVjhbbT022DgiuEAi;Port=5432;SslMode=Require");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ActorRelationships>(entity =>
        {
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("NOW()");
        });

        modelBuilder.Entity<Actors>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Actors__3214EC07800CDA63");

            entity.Property(e => e.ActorType)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("NOW()");
            entity.Property(e => e.DeletedAt).HasPrecision(0);
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.FullName).HasMaxLength(255);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Phone)
                .HasMaxLength(50)
                .IsUnicode(false);

            entity.HasOne(d => d.DeletedByActor).WithMany(p => p.InverseDeletedByActor)
                .HasForeignKey(d => d.DeletedByActorId)
                .HasConstraintName("FK_Actors_DeletedBy");
        });

        modelBuilder.Entity<ChatMessages>(entity =>
        {
            entity.HasIndex(e => new { e.ChatId, e.CreatedAt }, "IX_ChatMessages_ChatId").IsDescending(false, true);

            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("NOW()");
            entity.Property(e => e.DeletedAt).HasPrecision(3);
            entity.Property(e => e.EditedAt).HasPrecision(3);
        });

        modelBuilder.Entity<ClientStatuses>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ClientSt__3214EC078DF59A22");

            entity.Property(e => e.Code).HasMaxLength(50);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("NOW()")
                .HasColumnType("timestamp with time zone");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(100);
        });

        modelBuilder.Entity<Clients>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Clients__3214EC07F54175F4");

            entity.Property(e => e.ClientCode)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("NOW()");
            entity.Property(e => e.Emoji).HasMaxLength(10);
            entity.Property(e => e.GuardianName)
                .HasMaxLength(150)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Companies>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Companie__3214EC073FCE0C3C");

            entity.Property(e => e.CreatedAt).HasPrecision(0);
            entity.Property(e => e.DeletedAt).HasColumnType("timestamp with time zone");
            entity.Property(e => e.Name)
                .HasMaxLength(150)
                .IsUnicode(false);
            entity.Property(e => e.TaxId)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<CompanyChats>(entity =>
        {
            entity.HasIndex(e => e.CompanyId, "IX_CompanyChats_CompanyId");

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("NOW()");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name)
                .HasMaxLength(150)
                .IsUnicode(false);
        });

        modelBuilder.Entity<EventTypes>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__EventTyp__3214EC07121FFA43");

            entity.Property(e => e.Color)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Icon)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<EventUsers>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__EventUse__3214EC076C6278E5");

            entity.HasIndex(e => new { e.EventId, e.UserId }, "UQ_EventUsers").IsUnique();
        });

        modelBuilder.Entity<Events>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Events__3214EC07F11D4114");

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("NOW()");
            entity.Property(e => e.Description)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.EndDate).HasPrecision(0);
            entity.Property(e => e.OtherType)
                .HasMaxLength(250)
                .IsUnicode(false);
            entity.Property(e => e.StartDate).HasPrecision(0);
            entity.Property(e => e.Title)
                .HasMaxLength(200)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Files>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Files__3214EC0708AB4B0B");

            entity.HasIndex(e => e.FolderId, "IX_Files_Folder");

            entity.HasIndex(e => new { e.FolderId, e.FileName }, "UX_Files_Folder_FileName")
                .IsUnique()
                .HasFilter("\"IsDeleted\" = false");

            entity.Property(e => e.DeletedAt).HasPrecision(0);
            entity.Property(e => e.FileName).HasMaxLength(255);
            entity.Property(e => e.UploadedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("NOW()");
        });

        modelBuilder.Entity<FilesTypes>(entity =>
        {
            entity.Property(e => e.Name)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<FolderTypes>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__FolderTy__3214EC07F5557733");

            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Folders>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Folders__3214EC072D95D424");

            entity.HasIndex(e => e.CompanyId, "IX_Folders_Company");

            entity.HasIndex(e => e.ParentFolderId, "IX_Folders_Parent");

            entity.HasIndex(e => e.Path, "IX_Folders_Path");

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("NOW()");
            entity.Property(e => e.DeletedAt).HasPrecision(0);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.Path).HasMaxLength(500);
        });

        modelBuilder.Entity<GoalTrackerCategories>(entity =>
        {
            entity.Property(e => e.Name)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<GoalTrackerItems>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__GoalTrac__3214EC07E07E4273");

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("NOW()");
            entity.Property(e => e.MasteryCriteria).HasMaxLength(100);
            entity.Property(e => e.Name).HasMaxLength(255);
        });

        modelBuilder.Entity<GoalTrackerStatus>(entity =>
        {
            entity.Property(e => e.Color)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("NOW()")
                .HasColumnType("timestamp with time zone");
            entity.Property(e => e.DeleteAt).HasColumnType("timestamp with time zone");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<GoalTrackers>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__GoalTrac__3214EC077E5C6660");

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("NOW()");
            entity.Property(e => e.DeleteAt).HasColumnType("timestamp with time zone");
        });

        modelBuilder.Entity<JobTitles>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__JobTitle__3214EC076C5254CF");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("NOW()")
                .HasColumnType("timestamp with time zone");
            entity.Property(e => e.Description)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .IsUnicode(false);
        });

        modelBuilder.Entity<LibraryCategories>(entity =>
        {
            entity.HasIndex(e => e.Slug, "UQ_LibraryCategories_Slug").IsUnique();

            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Slug).HasMaxLength(50);
        });

        modelBuilder.Entity<LibraryItemFiles>(entity =>
        {
            entity.HasIndex(e => e.LibraryItemId, "IX_LibraryItemFiles_ItemId").HasFilter("\"IsDeleted\" = false");

            entity.HasOne(d => d.LibraryItem).WithMany(p => p.LibraryItemFiles)
                .HasForeignKey(d => d.LibraryItemId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.Property(e => e.BlobPath).HasMaxLength(1000);
            entity.Property(e => e.ContentType)
                .HasMaxLength(200)
                .HasDefaultValue("");
            entity.Property(e => e.FileName).HasMaxLength(500);
            entity.Property(e => e.UploadedAt).HasDefaultValueSql("NOW()");
        });

        modelBuilder.Entity<LibraryItems>(entity =>
        {
            entity.HasIndex(e => e.CategoryId, "IX_LibraryItems_CategoryId").HasFilter("\"IsDeleted\" = false");

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("NOW()");
            entity.Property(e => e.DeletedAt).HasPrecision(0);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(255);

            entity.HasOne(d => d.Category).WithMany(p => p.LibraryItems)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_LibraryItems_Category");
        });

        modelBuilder.Entity<Menus>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Menus__3214EC07846E3A0D");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
            entity.Property(e => e.Icon).HasMaxLength(100);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Route).HasMaxLength(255);
            entity.Property(e => e.Title).HasMaxLength(100);
        });

        modelBuilder.Entity<MessageReads>(entity =>
        {
            entity.HasIndex(e => e.MessageId, "IX_MessageReads_MessageId");

            entity.HasIndex(e => e.UserId, "IX_MessageReads_UserId");

            entity.Property(e => e.ReadAt)
                .HasPrecision(3)
                .HasDefaultValueSql("NOW()");
        });

        modelBuilder.Entity<NoteCategories>(entity =>
        {
            entity.HasNoKey();

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("NOW()");
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .IsUnicode(false);
        });

        modelBuilder.Entity<NotePriorities>(entity =>
        {
            entity.Property(e => e.Name)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<NoteSections>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__NoteSect__3214EC07A52B3FCB");

            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .IsUnicode(false);
        });

        modelBuilder.Entity<NoteTypes>(entity =>
        {
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Name)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Notes>(entity =>
        {
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("NOW()");
            entity.Property(e => e.DueDate).HasPrecision(0);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Title).HasMaxLength(200);
        });

        modelBuilder.Entity<RelationshipType>(entity =>
        {
            entity.Property(e => e.Name)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<SessionNotesStatus>(entity =>
        {
            entity.Property(e => e.Color)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("NOW()")
                .HasColumnType("timestamp with time zone");
            entity.Property(e => e.DeleteAt).HasColumnType("timestamp with time zone");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<SessionsNotes>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_Sessions");

            entity.Property(e => e.Actions);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("NOW()");
            entity.Property(e => e.DeletedAt).HasPrecision(0);
            entity.Property(e => e.Notes);
            entity.Property(e => e.SessionDate).HasPrecision(0);
        });

        modelBuilder.Entity<Staff>(entity =>
        {
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("NOW()");
        });

        modelBuilder.Entity<StaffDocumentTypes>(entity =>
        {
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .IsUnicode(false);
        });

        modelBuilder.Entity<StaffRoles>(entity =>
        {
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .IsUnicode(false);
        });

        modelBuilder.Entity<StaffStatus>(entity =>
        {
            entity.Property(e => e.Name)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<StaffTimeOff>(entity =>
        {
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("NOW()");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Reason).HasMaxLength(500);
        });

        modelBuilder.Entity<StorageEntities>(entity =>
        {
            entity.Property(e => e.Name)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<TimeOffStatus>(entity =>
        {
            entity.Property(e => e.Name)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<TimeOffTypes>(entity =>
        {
            entity.Property(e => e.Name)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<UserDelegations>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__UserDele__3214EC073B321500");

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("NOW()");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<UserTypeMenus>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__UserType__3214EC07707BCDD4");

            entity.Property(e => e.AssignedAt)
                .HasDefaultValueSql("NOW()")
                .HasColumnType("timestamp with time zone");
        });

        modelBuilder.Entity<UserTypes>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__UserType__3214EC07C9522850");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("NOW()")
                .HasColumnType("timestamp with time zone");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(100);
        });

        modelBuilder.Entity<Users>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Users__3214EC0777CCE6C5");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("NOW()")
                .HasColumnType("timestamp with time zone");
        });

        modelBuilder.Entity<library_permissions>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__library___3213E83F3BE29BF9");

            entity.HasIndex(e => e.actorId, "UQ_library_permissions_actor").IsUnique();

            entity.Property(e => e.assignedAt)
                .HasDefaultValueSql("NOW()")
                .HasColumnType("timestamp with time zone");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
