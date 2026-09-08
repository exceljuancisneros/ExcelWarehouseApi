using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// Autenticación delegada a ExcelIDPManager (OIDC). El access token es un JWT firmado sin cifrar
// (DisableAccessTokenEncryption del lado del IdP), así que se valida localmente contra las
// llaves públicas que expone /.well-known/openid-configuration — sin necesidad de una clave
// simétrica compartida ni de contactar al IdP en cada request.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Oidc:Authority"];
        options.RequireHttpsMetadata = false; // servidor de pruebas sin HTTPS

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Oidc:Authority"],
            ValidateAudience = false, // ExcelIDPManager no emite 'aud' — no hay resources configurados todavía
            NameClaimType = "name",
            RoleClaimType = "role"
        };
        options.MapInboundClaims = false;
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
