# Origen SR Connector

Conector local de sólo lectura para SoftRestaurant 11. Consulta ventas cerradas recientes, carga sus pagos y productos por separado y guarda cada payload en una Outbox SQLite persistente. Un cliente mock procesa la cola y escribe el JSON en logs; todavía no llama a una API real.

## Requisitos

- Windows 10/11 o Windows Server con [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) para desarrollo.
- Acceso de red a SQL Server.
- Una cuenta SQL Server con permisos únicamente de lectura sobre los objetos documentados de SoftRestaurant.

## Configuración

No escriba secretos en `appsettings.json`. Puede usar uno de estos mecanismos:

1. Copie `src/Origen.SRConnector/appsettings.json` como `src/Origen.SRConnector/appsettings.Local.json` y agregue ahí la cadena local. Ese archivo está ignorado por Git.
2. En producción, use la variable de entorno `SoftRestaurant__ConnectionString`.

Ejemplo de archivo local (reemplace los valores según su instalación):

```json
{
  "SoftRestaurant": {
    "ConnectionString": "Server=SERVIDOR;Database=NOMBRE_REAL;User ID=lector_origen;Password=SECRETO;Encrypt=True;TrustServerCertificate=True;",
    "PollingIntervalSeconds": 10,
    "LookbackHours": 48,
    "CommandTimeoutSeconds": 30
  },
  "Connector": {
    "LocationId": "origen-playa",
    "DatabasePath": "C:\\Origen\\data\\connector.db",
    "DispatchIntervalSeconds": 5,
    "DispatchBatchSize": 20
  }
}
```

`NOMBRE_REAL` es intencionalmente un marcador: el nombre de la base no fue proporcionado y no se asume. Para autenticación integrada puede configurar una cadena apropiada con `Integrated Security=True`; la identidad que ejecute el proceso debe tener acceso de lectura.

La configuración de .NET usa doble guion bajo para propiedades anidadas:

```powershell
$env:SoftRestaurant__ConnectionString = "Server=SERVIDOR;Database=NOMBRE_REAL;..."
$env:SoftRestaurant__PollingIntervalSeconds = "10"
$env:SoftRestaurant__LookbackHours = "48"
```

La aplicación nunca registra la cadena de conexión ni la API key. Aun así, prefiera un usuario SQL dedicado con permisos `SELECT` solamente.

## Compilar y probar

Desde la raíz del repositorio:

```powershell
dotnet restore
dotnet build Origen.SRConnector.sln
dotnet test Origen.SRConnector.sln
```

## Probar SQL Server

```powershell
dotnet run --project src/Origen.SRConnector -- test-sql
```

En caso de éxito muestra `SQL connection successful`. El comando ejecuta únicamente `SELECT 1`.

## Consultar estado

```powershell
dotnet run --project src/Origen.SRConnector -- status
```

Muestra la conectividad SQL, el modo de API, conteos de la Outbox, el último ticket detectado y la última sincronización exitosa. En esta versión la API aparece como `Mock (HTTP disabled)`.

Para verificar explícitamente el cliente configurado:

```powershell
dotnet run --project src/Origen.SRConnector -- test-api
```

El cliente mock no realiza ninguna solicitud HTTP.

## Ejecutar el polling

```powershell
dotnet run --project src/Origen.SRConnector -- run
```

El proceso consulta la ventana configurada, inserta ventas nuevas en SQLite y procesa la Outbox mediante el cliente mock. Una venta ya registrada no se vuelve a insertar aunque aparezca en ciclos posteriores.

En Windows se recomienda configurar `DatabasePath` con una ruta absoluta y persistente. La carpeta debe existir o poder ser creada por la identidad que ejecute el conector. No guarde `connector.db` en una carpeta temporal.

La identidad local de cada venta es:

```text
source + location_id + ticket
```

Al iniciar, cualquier registro que hubiera quedado en estado `sending` se devuelve a `pending`. Los fallos se conservan y reintentan después de 1, 5, 15, 30 y 60 minutos; intentos posteriores mantienen una espera de 60 minutos.

Para generar el ejecutable:

```powershell
dotnet publish src/Origen.SRConnector -c Release -r win-x64 --self-contained true -o publish
.\publish\origen-sr-connector.exe test-sql
.\publish\origen-sr-connector.exe run
```

La publicación `win-x64` genera un ejecutable autocontenido de un solo archivo. `appsettings.json` permanece junto al ejecutable. Coloque manualmente un `appsettings.Local.json` protegido junto al `.exe`; este archivo no se incluye automáticamente porque contiene secretos.

Detenga el modo consola con `Ctrl+C`; el Worker respeta la cancelación y termina limpiamente.

## Windows Service (preparado, no activar con la API mock en producción)

No ejecute el servicio desde la carpeta de GitHub. Use una carpeta estable, por ejemplo:

```text
C:\Origen\SRConnector
```

Configure la Outbox del servicio fuera del código fuente:

```json
{
  "Connector": {
    "LocationId": "origen-playa",
    "DatabasePath": "C:\\ProgramData\\Origen\\SRConnector\\connector.db",
    "DispatchIntervalSeconds": 5,
    "DispatchBatchSize": 20
  }
}
```

Primero pruebe el ejecutable publicado en consola. Después, en PowerShell como administrador, el servicio puede registrarse así:

```powershell
sc.exe create OrigenSRConnector binPath= '"C:\Origen\SRConnector\origen-sr-connector.exe" run' start= auto DisplayName= "Origen SR Connector"
sc.exe description OrigenSRConnector "Conector de ventas SoftRestaurant a Origen Loyalty"
sc.exe failure OrigenSRConnector reset= 86400 actions= restart/60000/restart/60000/restart/60000
sc.exe start OrigenSRConnector
sc.exe query OrigenSRConnector
```

Para detenerlo:

```powershell
sc.exe stop OrigenSRConnector
```

Para eliminar únicamente el registro del servicio después de detenerlo:

```powershell
sc.exe delete OrigenSRConnector
```

Los logs del servicio se consultan en **Visor de eventos → Registros de Windows → Aplicación**. La cuenta que ejecute el servicio debe tener acceso de lectura a SQL Server y escritura sobre la carpeta de `connector.db`. Si la cadena usa `Integrated Security=True`, la identidad del servicio necesita permisos SQL explícitos.

El soporte se implementa mediante el Worker Service oficial de .NET y `Microsoft.Extensions.Hosting.WindowsServices`; el apagado del servicio propaga `CancellationToken` a ambos Workers.

## Garantía de sólo lectura

Toda interacción con SoftRestaurant vive en `Infrastructure/SoftRestaurant`. El contrato sólo expone prueba de conexión y lectura de ventas. Las consultas:

- enumeran columnas explícitamente;
- empiezan con `SELECT`;
- usan `@since` y `@folio` como parámetros;
- aplican timeout y `CancellationToken`;
- no contienen operaciones de escritura o DDL.

El repositorio además rechaza cualquier texto de comando que no comience con `SELECT`. Esto complementa, pero no reemplaza, la protección más importante: usar una cuenta SQL Server con permisos exclusivamente de lectura.

## Decisiones y TODO

- Se consultan todas las estaciones; no existe filtro por `estacion`.
- Las fechas se serializan con el valor local entregado por SQL Server, sin inventar una zona horaria.
- Los renglones principales y modificadores se conservan individualmente.
- `preciocatalogo` se publica como `unit_price`; `calcpreciomenosdescuento`, `iva`, `cardBrand`, mesa y usuarios se conservan en el modelo interno, pero no se incluyen en el payload conceptual actual.
- `tempcheques.numcheque` se publica como `ticket` y es el identificador histórico global confirmado; `tempcheques.folio` se publica como `folio` y es el número operativo por turno. `WorkspaceId` no se consulta ni se publica.
- SQL Server 2014 SP1 se conserva por compatibilidad obligatoria con SoftRestaurant; la negociación TLS 1.0 es una limitación conocida del entorno local.
- TODO Fase 3: implementar el cliente HTTP y autenticación cuando se defina el contrato de Loyalty API.
- TODO posterior al corte: confirmar identidad y relaciones en `cheques`, `chequespagos` y `vwrepproductosvendidoscheques` antes de implementar reconciliación histórica.
