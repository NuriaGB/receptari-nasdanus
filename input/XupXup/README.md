# 🍳 Recetari — Blazor WebAssembly

Receptari personal amb planificador de menús. Les dades es guarden al **Google Drive propi de l'usuari** — sense servidor, sense base de dades, sense costos.

---

## 🏗️ Arquitectura

```
Navegador (Blazor WASM)
    ↓ OAuth2 (Google Identity Services)
Google Drive API  →  recetari_data.json  (al Drive de l'usuari)
```

- **Blazor WebAssembly** — corre al navegador, es desplega com fitxers estàtics al teu hosting
- **MudBlazor** — components UI professionals
- **Google Drive API** — emmagatzematge gratuït, privat, accessible des de qualsevol dispositiu

---

## 🛠️ Prerequisits

| Eina | Versió |
|---|---|
| .NET SDK | 8.0+ |
| Visual Studio 2022 o VS Code | qualsevol |
| Compte Google | per a les credencials OAuth |

---

## 🔑 Configuració de Google Cloud Console (un cop, ~5 min)

1. Ves a [console.cloud.google.com](https://console.cloud.google.com)
2. Crea un projecte nou → "Recetari"
3. **APIs & Services → Library** → activa **Google Drive API**
4. **APIs & Services → Credentials → Create Credentials → OAuth 2.0 Client ID**
   - Application type: **Web application**
   - Authorized JavaScript origins:
     - `http://localhost:5000` (desenvolupament)
     - `https://el-teu-domini.com` (producció)
   - Authorized redirect URIs: (deixar buit per a PKCE)
5. Copia el **Client ID** generat

6. Edita `wwwroot/appsettings.json`:
```json
{
  "Google": {
    "ClientId": "123456789-xxxx.apps.googleusercontent.com"
  }
}
```

> ⚠️ El Client ID és públic (va al navegador). **Mai posis el Client Secret** en un projecte WASM.

---

## 🚀 Executar en local

```bash
cd RecetariBlazor
dotnet run
# Obre http://localhost:5000
```

---

## 📦 Desplegar al teu hosting

```bash
dotnet publish -c Release -o ./publish
```

Copia el contingut de `publish/wwwroot/` al teu hosting (FTP, cPanel, etc.).

**Configura el servidor per redirigir totes les rutes a `index.html`** (necessari per Blazor WASM SPA):

### Apache (.htaccess)
```apache
RewriteEngine On
RewriteCond %{REQUEST_FILENAME} !-f
RewriteCond %{REQUEST_FILENAME} !-d
RewriteRule . /index.html [L]
```

### Nginx
```nginx
location / {
    try_files $uri $uri/ /index.html;
}
```

---

## 📁 Estructura del projecte

```
RecetariBlazor/
├── Models/
│   └── Models.cs              ← Tots els models (Recipe, Menu, Category...)
├── Services/
│   ├── GoogleDriveService.cs  ← OAuth2 + lectura/escriptura al Drive
│   ├── AppState.cs            ← Estat central de l'app (singleton)
│   └── IngredientScalingService.cs ← Lògica reescalat
├── Components/
│   ├── Layout/
│   │   └── MainLayout.razor   ← Navbar + drawer lateral
│   └── Pages/
│       ├── LoginPage.razor    ← Pantalla d'inici de sessió
│       ├── RecipeList.razor   ← Llista de receptes amb cerca i filtres
│       ├── RecipeDetail.razor ← Detall complet d'una recepta
│       ├── RecipeEdit.razor   ← Crear/editar recepta (formulari complet)
│       ├── StepByStep.razor   ← Mode pas a pas ⭐
│       ├── ScalingPage.razor  ← Ajust de quantitats ⭐
│       ├── MenuList.razor     ← Llista de menús
│       └── MenuEdit.razor     ← Editor de menú setmanal ⭐
├── wwwroot/
│   ├── index.html             ← Entry point + Google Identity Services
│   ├── appsettings.json       ← 👈 POSA EL CLIENT ID AQUÍ
│   ├── css/app.css            ← Estils globals
│   └── js/google-auth.js      ← Helper OAuth2 JS
├── _Imports.razor             ← Using globals
├── App.razor                  ← Router + tema MudBlazor
└── Program.cs                 ← DI setup
```

---

## 🔒 Privacitat i seguretat

- Les receptes es guarden **exclusivament al Google Drive de l'usuari**
- L'app només demana el permís `drive.file` (accés **únicament** als fitxers que ella mateixa crea)
- Cap dada passa per cap servidor extern
- El token OAuth es guarda a `sessionStorage` (s'esborra en tancar el navegador)

---

## 📱 Ús mòbil

Blazor WASM funciona al navegador mòbil. Per una experiència millor:
- Chrome per Android: menú → "Afegir a la pantalla d'inici" → funciona com una app nativa (PWA)
- Safari per iOS: botó compartir → "Afegir a l'inici"

---

## ✅ Funcionalitats implementades

| Funcionalitat | Estat |
|---|---|
| Login amb Google Drive | ✅ |
| Llista de receptes amb cerca | ✅ |
| Filtres per categoria | ✅ |
| Crear / editar recepta | ✅ |
| Ingredients, material, passos, consells | ✅ |
| Foto de recepta (base64) | ✅ |
| Mode pas a pas | ✅ |
| Ajust de quantitats intel·ligent | ✅ |
| Planificador de menús setmanal | ✅ |
| Crear recepta nova des del menú | ✅ |
| Guardat automàtic al Drive | ✅ |
| Export / Import JSON | ✅ (via Drive) |
