# Asset Management Strategy - Tone & Beats by Hostility

**Versión:** 1.0  
**Última actualización:** v1.0.4 (2026-04-09)  
**Crítica:** Este documento es esencial para mantener la integridad de assets en futuras compilaciones

---

## Problema Identificado (v1.0.3 → v1.0.4)

En v1.0.3, la imagen QR (`qrdonaciones.png`) configurada como `Content` con `CopyToOutputDirectory` se perdía en ciertos escenarios de compilación:
- ✗ Desaparecía en compilaciones Single-File Executable
- ✗ No se incluía fiablemente en instaladores
- ✗ Requería reparación manual después de cada build

**Raíz del problema:** Los assets configurados como `Content` con `CopyToOutputDirectory` no son confiables en publicaciones autónomas (self-contained) y pueden perderse durante optimizaciones de compilación.

---

## Solución Implementada (v1.0.4+)

### A. Estrategia EmbeddedResource (Crítico)

**Aplicado a:**
- ✓ `Assets/qrdonaciones.png` (v1.0.4) - **CRÍTICO**

**Configuración en csproj:**
```xml
<EmbeddedResource Include="Assets\qrdonaciones.png" />
```

**Beneficios:**
- ✓ Incrustado directamente en el assembly compilado (.dll/.exe)
- ✓ Garantizado al 100% en todos los escenarios: Debug, Release, Single-File, Installer
- ✓ No depende de archivos externos copiados
- ✓ Mejora portabilidad y distribución

**Implementación en Code-Behind (AboutWindow.xaml.cs):**

```csharp
/// <summary>
/// Carga la imagen QR incrustada (EmbeddedResource) desde el assembly.
/// Garantiza disponibilidad en todos los escenarios de build.
/// </summary>
private void LoadEmbeddedImage()
{
    try
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "AudioAnalyzer.Assets.qrdonaciones.png";
        
        using (var stream = assembly.GetManifestResourceStream(resourceName))
        {
            if (stream == null)
            {
                LoggerService.Log("ERROR: Recurso no encontrado: " + resourceName, "ERROR");
                return;
            }
            
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = stream;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            
            if (FindName("QrImage") is Image qrImage)
            {
                qrImage.Source = bitmap;
                LoggerService.Log("✓ Imagen QR cargada desde recurso incrustado");
            }
        }
    }
    catch (Exception ex)
    {
        LoggerService.Log($"ERROR LoadEmbeddedImage: {ex.Message}", "ERROR");
    }
}
```

**XAML:**
```xml
<!-- NO usar Source="/Assets/qrdonaciones.png" (Pack URI) -->
<!-- Usar x:Name para acceso desde code-behind -->
<Image x:Name="QrImage" Grid.Row="5" Height="280" HorizontalAlignment="Center" />
```

---

### B. Build Validation Task (Prevención)

**Configuración en csproj (Target pre-build):**

```xml
<Target Name="ValidateCriticalAssets" BeforeTargets="Build">
    <Message Text="[Asset Validation] Checking critical embedded resources..." Importance="high" />
    <Error Condition="!Exists('Assets\qrdonaciones.png')" 
           Text="CRITICAL: Assets\qrdonaciones.png not found." />
    <Message Text="[Asset Validation] ✓ All critical assets present" Importance="high" />
</Target>
```

**Funcionamiento:**
- Se ejecuta **ANTES** de cada compilación
- Verifica que `Assets/qrdonaciones.png` existe en el filesystem
- **FALLA la compilación** si el archivo no se encuentra
- Previene que se compile sin los assets críticos

**Ventajas:**
- ✓ Fail-fast: detección inmediata de problemas
- ✓ Imposible compilar sin assets críticos
- ✓ Aplica a todos los modos: Debug, Release, Single-File
- ✓ Previene regresiones automáticamente

---

## Clasificación de Assets

### Tier 1: CRÍTICO (EmbeddedResource)
Estos assets **NUNCA** deben perderse. Requieren `EmbeddedResource`.

| Asset | Ubicación | Motivo | Estado v1.0.4 |
|-------|-----------|--------|----------------|
| `qrdonaciones.png` | `src/Assets/` | Mostrado en AboutWindow | ✓ EmbeddedResource |

### Tier 2: IMPORTANTE (Content + CopyToOutputDirectory)
Estos assets pueden ser externos pero deben copiarse confiablemente.

| Asset | Ubicación | Motivo | Estado v1.0.4 |
|-------|-----------|--------|----------------|
| `HOST_BLANCO.png` | `src/Assets/` | Branding en AboutWindow | Content |
| `HOST_NEGRO.png` | `src/Assets/` | Tema oscuro alternativo | Content |
| `HOSTBLANCO.ico` | `src/Assets/` | Ícono aplicación | Content |
| `ffmpeg.exe` | `publish/ffmpeg/` | Procesamiento audio externo | Content |
| `ffprobe.exe` | `publish/ffmpeg/` | Inspección audio externa | Content |

### Tier 3: DATOS (Runtime Generated)
Generados en tiempo de ejecución, no versionados.

| Elemento | Ubicación | Motivo |
|----------|-----------|--------|
| Logs | `%LocalAppData%/Hostility/Tone&Beats/logs/` | Debugging runtime |
| Caché análisis | Temp local | Optimización sesión |
| Configuración usuario | Registry/JSON local | Preferencias |

---

## Procedimiento para Agregar Nuevos Assets Críticos

Si en futuras versiones necesitas agregar un asset que **no puede perderse nunca**:

### Paso 1: Categorizar
¿El asset es:
- **Crítico** (UI esencial, sin fallback): → EmbeddedResource
- **Importante** (funcional, puede degradarse): → Content + CopyToOutputDirectory
- **Opcional** (mejora visual, dispensable): → Content

### Paso 2: Si es CRÍTICO
1. Agregar a `csproj`:
   ```xml
   <EmbeddedResource Include="Assets\nuevo_asset.png" />
   ```

2. Crear método de carga en code-behind (como `LoadEmbeddedImage()`):
   ```csharp
   var resourceName = "AudioAnalyzer.Assets.nuevo_asset.png";
   using (var stream = assembly.GetManifestResourceStream(resourceName))
   {
       // Cargar como en LoadEmbeddedImage()
   }
   ```

3. Agregar validación en csproj:
   ```xml
   <Error Condition="!Exists('Assets\nuevo_asset.png')" 
          Text="CRITICAL: Assets\nuevo_asset.png not found." />
   ```

4. Actualizar esta documentación

### Paso 3: Validar
- [ ] Compila en Debug
- [ ] Compila en Release
- [ ] Genera Single-File ejecutable exitosamente
- [ ] Instala sin errores
- [ ] Asset disponible en todas las instancias

---

## Testing Procedure (Post-Implementation)

Después de modificar cualquier asset:

### Escenario 1: Debug Build
```powershell
cd src
dotnet build -c Debug
# Verificar: Asset disponible, no errores
```

### Escenario 2: Release Build
```powershell
cd src
dotnet build -c Release
# Verificar: Asset disponible, sin warnings
```

### Escenario 3: Single-File Executable
```powershell
cd src
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true
# Verificar: .exe ~370 MB, asset funcional
```

### Escenario 4: Instalador Inno Setup
```powershell
# Abrir Inno Setup
# Compactar el single-file .exe
# Verificar: Asset funcional post-instalación
```

---

## Checklist para Release Futuro

Antes de cada release que involucre compilación:

- [ ] Build Validation Task ejecutado sin errores
- [ ] Assets críticos presentes en `src/Assets/`
- [ ] Compilación Debug exitosa
- [ ] Compilación Release exitosa
- [ ] Single-File executable generado correctamente
- [ ] Instalador generado correctamente
- [ ] Assets críticos verificados en instalado
- [ ] Git: cambios en csproj commiteados con descripción "chore: updated assets for vX.X.X"

---

## Historiales de Cambios

### v1.0.4 (2026-04-09)
- **Agregado:** EmbeddedResource para `qrdonaciones.png`
- **Agregado:** Build Validation Task en csproj
- **Agregado:** Método `LoadEmbeddedImage()` en AboutWindow.xaml.cs
- **Razón:** Resolver pérdida de QR en single-file y instaladores
- **Status:** ✓ COMPLETADO Y VALIDADO

### v1.0.3 (Previo)
- **Problema:** Assets como Content se pierden en ciertos escenarios
- **Síntoma:** QR falta en AboutWindow post-compilación
- **Raíz:** CopyToOutputDirectory no confiable en publish autónomas

---

## Referencias Técnicas

### WPF Pack URI vs EmbeddedResource
| Aspecto | Pack URI | EmbeddedResource |
|--------|----------|------------------|
| Sintaxis | `/Assets/file.png` | Assembly.GetManifestResourceStream() |
| Ubicación | Filesystem (relativa o assembly) | Compilado en assembly |
| Single-File | ✗ Inconsistente | ✓ Garantizado |
| Performance | ✗ Acceso disco | ✓ Cargado en RAM |
| Distribución | ✗ Requiere archivos extra | ✓ Todo-en-uno |

### Assembly Resource Names
El nombre del recurso incrustado sigue patrón: `{RootNamespace}.{RelativePath}`

Ejemplo:
- Archivo: `src/Assets/qrdonaciones.png`
- RootNamespace: `AudioAnalyzer`
- Nombre recurso: `AudioAnalyzer.Assets.qrdonaciones.png`

Para verificar nombres disponibles:
```powershell
$dll = [System.Reflection.Assembly]::LoadFrom("bin/Release/net8.0-windows/ToneAndBeatsByHostility.dll")
$dll.GetManifestResourceNames() | Where-Object { $_ -match "Assets" }
```

---

## Contacto / Soporte

Para problemas con assets en futuras compilaciones:
1. Revisar esta documentación
2. Verificar que archivos existen en `src/Assets/`
3. Revisar Build Validation Task en csproj
4. Re-compilar con `dotnet clean && dotnet build`

---

**Documento crítico para mantenimiento futuro. Preservar en repositorio.**
