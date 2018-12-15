﻿using FluentValidation.AspNetCore;
using ListsAPI.Features.Lists.Authorizers;
using ListsAPI.Features.Lists.DataAccess;
using ListsAPI.Features.Profile.DataAccess;
using ListsAPI.Features.TodoItems.DataAccess;
using ListsAPI.Infrastructure;
using ListsAPI.Infrastructure.Authentication;
using ListsAPI.Infrastructure.Database;
using ListsAPI.Infrastructure.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Swashbuckle.AspNetCore.Swagger;
using System;
using System.Text;

namespace ListsAPI
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<ListContext>(options => options.UseSqlServer(Configuration.GetConnectionString("DefaultConnection")));

            services.AddScoped<ITodoItemsWriter, TodoItemsWriter>();
            services.AddScoped<ITodoItemsReader, TodoItemsReader>();
            services.AddScoped<IListWriter, ListWriter>();
            services.AddScoped<IListReader, ListReader>();
            services.AddScoped<IUserProfileWriter, UserProfileWriter>();
            services.AddScoped<IUserProfileReader, UserProfileReader>();
            services.AddScoped<IEmailMessenger, EmailMessenger>();
            services.AddScoped<IConfigurationValueProvider, ConfigurationValueProvider>();
            services.AddScoped<IUserProfileTokenWriter, UserProfileTokenWriter>();
            services.AddScoped<IUserProfileTokenReader, UserProfileTokenReader>();
            services.AddScoped<IAzureStorageManager, AzureStorageManager>();
            services.AddScoped<IDatabaseConnectionProvider, DatabaseConnectionProvider>();
            services.AddScoped<IListAuthoriser, ListAuthoriser>();
            services.AddScoped<IAuthenticationTokenProvider, AuthenticationTokenProvider>();

            services.AddCors();

            services.AddMvc()
                .AddFluentValidation(fvc =>
                    fvc.RegisterValidatorsFromAssemblyContaining<Startup>())
                .SetCompatibilityVersion(CompatibilityVersion.Version_2_1);

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new Info { Title = "Shine", Version = "v1" });
            });

            ConfigureAuthentication(services);
        }

        public static void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseHsts();
            }

            app.UseCors(
                options => options.WithOrigins("http://localhost:8080").AllowAnyMethod().AllowAnyOrigin().AllowAnyHeader()
            );

            app.UseSwagger();

            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Shine (V1)");
            });

            using (var serviceScope = app.ApplicationServices.GetRequiredService<IServiceScopeFactory>().CreateScope())
            {
                var context = serviceScope.ServiceProvider.GetService<ListContext>();
                context.Database.Migrate();
            }

            app.UseHttpsRedirection();
            app.UseAuthentication();
            app.UseMvc();
        }

        private void ConfigureAuthentication(IServiceCollection services)
        {
            var symmetricSecurityKey = Configuration.GetValue<string>("SymmetricSecurityKey");

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "JwtBearer";
                options.DefaultChallengeScheme = "JwtBearer";
            })

            .AddJwtBearer("JwtBearer", jwtBearerOptions =>
            {
                jwtBearerOptions.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(symmetricSecurityKey)),
                    ValidateIssuer = true,
                    ValidIssuer = "ListsApi",
                    ValidateAudience = true,
                    ValidAudience = "ListsWeb",
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(5)
                };
            });
        }
    }
}