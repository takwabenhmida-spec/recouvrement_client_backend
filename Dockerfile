# ============================================================
# ÉTAPE 1 : BUILD — Compiler le code C# avec le SDK .NET
# ============================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copier le fichier projet et restaurer les dépendances NuGet
COPY RecouvrementAPI.csproj .
RUN dotnet restore

# Copier tout le code source et compiler en mode Release
# NOTE : Assurez-vous d'avoir un fichier .dockerignore à côté de ce Dockerfile

RUN dotnet publish -c Release -o /app/publish

# ============================================================
# ÉTAPE 2 : RUNTIME — Image légère pour exécuter l'API
# ============================================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Copier uniquement les fichiers compilés
COPY --from=build /app/publish .

# Créer le dossier uploads et ajuster les permissions pour l'utilisateur non-root
RUN mkdir -p /app/uploads && chown -R $APP_UID:$APP_UID /app/uploads

# Exposer le port de l'API
EXPOSE 5203

# Utiliser l'utilisateur non-privilégié (Correctif Security Hotspot)
USER $APP_UID

# Lancer l'API au démarrage du conteneur
ENTRYPOINT ["dotnet", "RecouvrementAPI.dll"]
