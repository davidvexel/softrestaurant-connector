# Origen SR Connector

Conector local de sólo lectura para SoftRestaurant 11. Consulta ventas cerradas recientes, carga sus pagos y productos por separado, guarda cada payload en una Outbox SQLite persistente y lo envía por HTTPS a Origen Platform. También conserva un modo mock para desarrollo.

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
  "Api": {
    "Mode": "Http",
    "BaseUrl": "https://app.origennatural.mx/",
    "ApiKey": "orp_TOKEN_DEL_AMBIENTE",
    "TimeoutSeconds": 20
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
$env:Api__Mode = "Http"
$env:Api__BaseUrl = "https://app.origennatural.mx/"
$env:Api__ApiKey = "orp_TOKEN_DEL_AMBIENTE"
```

La aplicación nunca registra la cadena de conexión ni la API key. Aun así, prefiera un usuario SQL dedicado con permisos `SELECT` solamente.

`Api:Mode` permanece en `Mock` dentro del archivo versionado como medida de seguridad. Active `Http` únicamente en `appsettings.Local.json` o mediante variables de entorno. El token Bearer sólo se adjunta a solicitudes HTTPS hacia los endpoints autenticados de Origen Platform.

Para una primera prueba HTTP use una base nueva sin borrar la Outbox del mock, por ejemplo:

```json
"DatabasePath": "C:\\Users\\origen\\Documents\\GitHub\\softrestaurant-connector\\data\\connector-http-test.db"
```

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

Muestra la conectividad SQL, el modo de API, conteos de la Outbox, el último ticket detectado y la última sincronización exitosa. La API aparece como `HTTP: Connected` cuando el health check público responde correctamente.

Para verificar explícitamente el cliente configurado:

```powershell
dotnet run --project src/Origen.SRConnector -- test-api
```

En modo `Http`, el comando llama a `GET /api/v1/connector` para validar dominio, HTTPS, token y sucursal; no envía ventas.

## Ejecutar el polling

```powershell
dotnet run --project src/Origen.SRConnector -- run
```

El proceso consulta la ventana configurada tanto en las tablas temporales como en las históricas, inserta ventas nuevas en SQLite y procesa la Outbox mediante el cliente configurado. Una venta ya registrada no se vuelve a insertar aunque aparezca en ciclos posteriores.

La reconciliación posterior al corte usa exclusivamente estas fuentes confirmadas:

- `cheques` para la cabecera;
- `chequespagos` para los pagos, relacionados mediante el folio histórico;
- `vwrepproductosvendidoscheques` para los productos, relacionados mediante el folio histórico.

`cheques.numcheque` conserva el ticket global. Como `cheques.folio` cambia durante el corte, el payload utiliza `cheques.foliotempcheques` como `folio`; si este último fuera nulo, utiliza el folio histórico como respaldo. Si un ticket aparece simultáneamente en las fuentes temporal e histórica durante el corte, se procesa una sola vez y se prefiere la versión temporal. La Outbox vuelve a deduplicar mediante `source + location_id + ticket`.

En Windows se recomienda configurar `DatabasePath` con una ruta absoluta y persistente. La carpeta debe existir o poder ser creada por la identidad que ejecute el conector. No guarde `connector.db` en una carpeta temporal.

La identidad local de cada venta es:

```text
source + location_id + ticket
```

Al iniciar, cualquier registro que hubiera quedado en estado `sending` se devuelve a `pending`. Timeouts, HTTP `408`, `429`, `5xx` y errores de red se reintentan después de 1, 5, 15, 30 y 60 minutos; intentos posteriores mantienen una espera de 60 minutos. Rechazos permanentes como `400`, `401`, `403`, `409` y `422` quedan en `failed` para revisión manual y no se reintentan automáticamente.

SoftRestaurant puede marcar un cheque como cerrado unos segundos antes de que sus pagos estén disponibles. Una venta sin productos o pagos no se encola; se vuelve a leer en el siguiente polling. Si un payload incompleto ya hubiera sido rechazado permanentemente, la Outbox lo reactiva únicamente cuando el payload persistido cambia con la información faltante.

Para generar el ejecutable:

```powershell
dotnet publish src/Origen.SRConnector -c Release -r win-x64 --self-contained true -o publish
.\publish\origen-sr-connector.exe test-sql
.\publish\origen-sr-connector.exe run
```

La publicación `win-x64` genera un ejecutable autocontenido de un solo archivo. `appsettings.json` permanece junto al ejecutable. Coloque manualmente un `appsettings.Local.json` protegido junto al `.exe`; este archivo no se incluye automáticamente porque contiene secretos. La aplicación siempre resuelve ambos archivos desde la carpeta del ejecutable, aunque PowerShell o Windows Service utilicen otro directorio de trabajo.

Detenga el modo consola con `Ctrl+C`; el Worker respeta la cancelación y termina limpiamente.

## Windows Service

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
- `numcheque` se publica como `ticket` y es el identificador histórico global confirmado. En la fuente temporal, `tempcheques.folio` se publica como `folio`; en la histórica se usa `cheques.foliotempcheques`, porque `cheques.folio` cambia durante el corte. `WorkspaceId` no se consulta ni se publica.
- SQL Server 2014 SP1 se conserva por compatibilidad obligatoria con SoftRestaurant; la negociación TLS 1.0 es una limitación conocida del entorno local.
- La API real usa `POST https://app.origennatural.mx/api/v1/sales` con autenticación Bearer. `200` y `201` se consideran éxito.
- La reconciliación histórica fue confirmada con datos posteriores al corte: pagos y productos se relacionan mediante `cheques.folio`, mientras `foliotempcheques` conserva el folio operativo original.
