## 🚀 Guía de Configuración y Ejecución de la API

Sigue estos pasos cuidadosamente para asegurar que la API arranque en el entorno correcto y con la configuración de red adecuada.

### 1️⃣ Paso 1: Confirmar el modo actual (sin mostrar secretos)

Primero, verificaremos que los servicios externos estén configurados en modo HTTP. 

Abre tu consola (CMD) y ejecuta los siguientes comandos:

```cmd
cd /d D:\GitHub\profesiograma\backend
dotnet user-secrets list --project src\App.Api | findstr /i "ServiciosExternos"
```
> 💡 **Nota:** El comando `findstr` filtra la salida para que la cadena de conexión con tu contraseña **no** aparezca en pantalla.

**Resultado esperado:**
```text
ServiciosExternos:Modo = Http
```

*   ✅ **Si aparece así:** Estás en modo `Http` y no necesitas cambiar nada.
*   ⚠️ **Si no aparece ninguna línea:** La API arrancará en modo `Simulado`. Para activar el modo HTTP, ejecuta:

    ```cmd
    dotnet user-secrets set "ServiciosExternos:Modo" "Http" --project src\App.Api
    ```
    *Esperado:* `Successfully saved ServiciosExternos:Modo = Http to the secret store.`

> ℹ️ **Referencia futura:** Si alguna vez necesitas volver al modo `Simulado` (no lo hagas ahora), puedes remover el secreto con:
> `dotnet user-secrets remove "ServiciosExternos:Modo" --project src\App.Api`

---

### 2️⃣ Paso 2: Detener la API

Asegúrate de que la API no esté corriendo actualmente:

*   💻 **En consola (si usaste `dotnet run`):** Ve a la ventana de la consola y presiona `Ctrl + C`. Espera a que vuelva el *prompt* (`D:\...>`). Es posible que veas el mensaje *Application is shutting down...*
*   🛠️ **En Visual Studio:** Ve al menú **Depurar** → **Detener depuración** (o presiona `Shift + F5`).

---

### 3️⃣ Paso 3: Iniciar la API de nuevo

#### Opción A: Desde la Consola
```cmd
cd /d D:\GitHub\profesiograma\backend
dotnet run --project src\App.Api --launch-profile https
```
> ⚠️ **Atención:** El nombre del perfil `https` está **[PENDIENTE DE CONFIRMAR]**. Si recibes un error del tipo *«launch profile not found»*, utiliza el comando habitual con el que inicias la API.

#### Opción B: Desde Visual Studio
Presiona `F5` (o `Ctrl + F5` para ejecutar sin depurar), asegurándote de tener seleccionado el perfil **https**.

#### 🎯 Resultado esperado en la consola:
```text
Now listening on: https://localhost:7180
Now listening on: http://localhost:5180
Application started. Press Ctrl+C to shut down.
```

🛑 **Revisión importante:** 
* No debe haber ningún error en rojo (como errores del sembrador o de conexión a la base de datos) antes de esas líneas. 
* Si solo aparece `http://localhost:5180` y no la `7180`, el perfil no es el correcto. El script llama al puerto `7180` (https) y fallará si no está activo.

---

### 4️⃣ Paso 4: Requisitos de Red

Dependiendo de tu ubicación, la conectividad hacia los servicios de empleados requiere configuración adicional:

*   🏢 **En la oficina:** No necesitas hacer nada más.
*   🏠 **Desde casa:** **Conecta la VPN Sophos** *antes* de correr el script. Sin la VPN, los procesos `c01` y `c02` responderán con un error `503 - Servicio de empleados no disponible`.

---

### ⏭️ Siguientes Pasos
Una vez completados estos 4 pasos, continúa con el **Paso 2 de la guía principal** (tomar la captura previa en SSMS) y luego ejecuta `cargos.cmd` **una sola vez**.