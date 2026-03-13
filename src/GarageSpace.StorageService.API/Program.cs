using GarageSpace.StorageService.Application.Services;
using GarageSpace.StorageService.Domain.Repositories;
using GarageSpace.StorageService.Infrastructure;
using GarageSpace.StorageService.Infrastructure.Data;
using GarageSpace.StorageService.Infrastructure.Data.Repositories;
using GarageSpace.StorageService.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database
builder.Services.AddDbContext<StorageProcessorDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// S3 configuration
builder.Services.Configure<S3Options>(builder.Configuration.GetSection(S3Options.SectionName));

// Infrastructure services
builder.Services.AddScoped<IS3Service, S3Service>();
builder.Services.AddScoped<IFileValidationService, FileValidationService>();

// Repositories
builder.Services.AddScoped<IFileMetadataRepository, FileMetadataRepository>();
builder.Services.AddScoped<IUploadSessionRepository, UploadSessionRepository>();

// Application services
builder.Services.AddScoped<IUploadService, UploadService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
