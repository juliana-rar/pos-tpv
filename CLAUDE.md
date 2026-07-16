## graphify

This project has a knowledge graph at graphify-out/ with god nodes, community structure, and cross-file relationships.

Rules:
- For codebase questions, first run `graphify query "<question>"` when graphify-out/graph.json exists. Use `graphify path "<A>" "<B>"` for relationships and `graphify explain "<concept>"` for focused concepts. These return a scoped subgraph, usually much smaller than GRAPH_REPORT.md or raw grep output.
- If graphify-out/wiki/index.md exists, use it for broad navigation instead of raw source browsing.
- Read graphify-out/GRAPH_REPORT.md only for broad architecture review or when query/path/explain do not surface enough context.
- After modifying code, run `graphify update .` to keep the graph current (AST-only, no API cost).

## Sistema POS para Pizzería (Blazor) — spec de producto y reglas de negocio

Aplicación de punto de venta (POS) para una pizzería/restaurante, desarrollada en **Blazor** (Server o WebAssembly — usar el modelo ya definido en el proyecto, no mezclar). Cubre todo el flujo operativo: toma de comandas, cocina, sala, gestión de mesas, reservas y facturación.

La aplicación debe funcionar correctamente tanto en **PC** (tablet de barra/caja) como en **móvil** (camareros tomando comandas desde la sala).

### Stack tecnológico

- **Blazor** (.NET) — componentes reutilizables, `@code` bien separado de la vista cuando el componente crece.
- **C#** para toda la lógica de negocio y servicios.
- **Base de datos relacional** vía Entity Framework Core (usar la que ya esté configurada en el proyecto). Procedimientos almacenados para las operaciones críticas (cierre de comandas, cálculo de facturación, fusión/separación de mesas) cuando la lógica sea puramente de datos y se beneficie de ejecutarse en el motor de BD.
- **Sin Bootstrap ni ningún framework CSS externo.** CSS propio, escrito a mano.
- **SignalR** (si el proyecto es Blazor Server) para sincronizar en tiempo real POS ↔ Cocina ↔ Camarero (una comanda nueva debe aparecer en cocina sin recargar).
- Librería de exportación a Excel ya elegida en el proyecto (p. ej. ClosedXML) para los informes de facturación.

### Convenciones de CSS (regla estricta)

Objetivo: **el mínimo número de clases/etiquetas posible**, con una etiqueta o clase "base" por bloque y variaciones resueltas con selectores `nth-child`, `nth-of-type`, `:first-child`, `:last-child`, etc., en lugar de crear una clase nueva por cada variante.

Reglas concretas:

- Cada componente de UI repetible (tarjeta de producto, fila de mesa, línea de comanda) tiene **una sola clase contenedora** (ej. `.product-card`, `.table-item`, `.order-line`). Las variaciones de color/categoría/estado se resuelven así:
  - Si son *N* elementos hermanos del mismo tipo (ej. categorías del menú lateral), usar `.category-list > div:nth-child(1)`, `:nth-child(2)`, etc., para colores/iconos, **no** `.category-drinks`, `.category-starters`, etc.
  - Si el estado es dinámico (mesa libre/ocupada/reservada), usar **atributos de datos** (`data-status="occupied"`) y seleccionar por atributo en CSS (`.table-item[data-status="occupied"]`) en vez de multiplicar clases.
- Nada de `div` anidados innecesarios: si un `div` no aporta estructura ni estilo, no existe. Preferir la menor profundidad de anidación posible.
- Variables CSS (`:root { --color-primary: ...; }`) para toda la paleta, tipografía y espaciados. Nada de colores hardcodeados repetidos.
- Un archivo CSS por pantalla/módulo (`pos.css`, `kitchen.css`, `waiter.css`, `tables.css`, `reservations.css`, `billing.css`) más un `base.css` global con reset, variables y utilidades mínimas compartidas.
- Grid/Flexbox nativos para todos los layouts (grillas de productos, mapa de mesas). Nada de frameworks de grid.
- Mobile-first: media queries `min-width` progresivas, no al revés.

### Módulos y pantallas

#### Punto de Venta (POS) — pantalla principal
- Layout de dos columnas: **izquierda** = lista de categorías (Bebidas, Entrantes, Primeros, Pizzas, Postres...), **centro/derecha** = grid de `div`s clicables con los productos de la categoría seleccionada.
- Al seleccionar una categoría en la izquierda, el grid central cambia sin recargar la página (estado local del componente).
- Cada producto es un bloque con imagen, nombre y precio.
- Para añadir un producto a una comanda, **la mesa debe estar abierta primero**. Si no hay mesa abierta/seleccionada, la app debe pedir abrir o seleccionar una mesa antes de permitir añadir líneas.

#### Cocina (KDS – Kitchen Display System)
- Vista de solo lectura orientada a tablet/pantalla en cocina.
- Lista o columnas de comandas activas agrupadas por mesa, con las líneas pendientes de preparar.
- Marcar líneas o comandas completas como "listo para servir" (esto debe notificar en tiempo real a la pantalla de camarero).
- Sin necesidad de menú lateral de categorías; diseño simplificado y de alto contraste, pensado para verse desde lejos.

#### Camarero (pantalla orientada a móvil)
- Vista simplificada para tomar comandas desde la sala.
- Ver mesas asignadas, añadir productos, marcar platos como servidos, avisar de comandas listas en cocina.
- Prioridad total al diseño responsive/táctil: botones grandes, poco scroll.

#### Editor de productos
- CRUD de productos: nombre, categoría, precio, imagen (subida o URL), disponible/no disponible.
- Reordenar productos dentro de una categoría.
- Gestión de categorías (crear, renombrar, reordenar, ocultar).

#### Mapa de mesas (editable)
- Representación visual del salón con las mesas como elementos posicionables (drag & drop o edición de coordenadas x/y).
- Formas de mesa editables (redonda, cuadrada, rectangular) y capacidad (nº de personas) editable.
- Permitir **juntar mesas** (unir dos o más mesas en una sola comanda/grupo) y **separar mesas** (deshacer la unión).
- Estados visuales claros por color/atributo `data-status`: libre, ocupada, reservada, unida a otra mesa.
- **Regla de negocio clave:** si una mesa tiene una reserva activa, no puede liberarse ni reasignarse hasta que la comanda asociada esté completamente pagada.

#### Comandas abiertas (gestión en curso)
- Listado de todas las comandas abiertas en este momento, con mesa, hora de apertura, total acumulado.
- Click en una comanda → abre el detalle para:
  - Añadir más bebida/comida.
  - Modificar cantidades o eliminar líneas.
  - Añadir comentarios por línea o por comanda (ej. "sin cebolla", "para llevar").
  - Cambiar la comanda de mesa (mover a otra mesa).
- Desde aquí también se inicia el cobro/cierre de la comanda.

#### Reservas
- Crear reserva: nombre del cliente, nº de personas, hora, duración estimada.
- Asignar la reserva a una o varias mesas del mapa (según personas/mesas disponibles a esa hora).
- Vista combinada con el mapa de mesas para ver disponibilidad en tiempo real según horario.
- Una reserva bloquea la mesa correspondiente en el mapa hasta que la comanda derivada se pague (ver Mapa de mesas).

#### Facturación / informes
- Pantalla con **calendario** para seleccionar día, rango de días o mes.
- Totales de facturación: por día, por mes, desglose por categoría de producto y/o por mesa si es útil.
- Exportación a **Excel profesional** (formato con cabeceras, estilos, totales, no un volcado plano de tabla) del periodo seleccionado.

### Flujo de apertura de mesa y comanda (regla de negocio central)

1. Se crea/selecciona la mesa (desde el mapa o desde POS).
2. Se abre la mesa → se genera una comanda vinculada.
3. Al abrir, por defecto se solicita indicar la **cantidad de entrantes** (o hay un paso rápido para cantidad de comensales que luego sugiere entrantes por defecto — ajustar según se defina en detalle de negocio, pero el enganche "abrir mesa → cantidad inicial de entrantes" debe estar presente antes de permitir añadir el resto de productos libremente).
4. A partir de ahí, se pueden añadir productos de cualquier categoría desde POS o desde la pantalla de camarero.
5. La comanda permanece "abierta" y editable (añadir, quitar, comentar, cambiar de mesa) hasta que se cobra.
6. Al cobrar, la comanda se cierra, la mesa queda libre (salvo que tenga otra reserva encima) y el importe pasa a los informes de facturación.

### Entidades principales (modelo de datos orientativo)

- `Producto` (Id, Nombre, CategoriaId, Precio, ImagenUrl, Disponible, Orden)
- `Categoria` (Id, Nombre, Orden, Visible)
- `Mesa` (Id, Nombre/Número, Forma, Capacidad, PosX, PosY, Estado, MesaAgrupadaId nullable para fusiones)
- `Reserva` (Id, NombreCliente, NumPersonas, FechaHora, DuracionMinutos, MesaId, Estado)
- `Comanda` (Id, MesaId, FechaApertura, FechaCierre, Estado, Total, Comentarios)
- `LineaComanda` (Id, ComandaId, ProductoId, Cantidad, PrecioUnitario, Comentario)
- `Pago` (Id, ComandaId, Importe, MetodoPago, Fecha)

Ajustar nombres/campos exactos a lo que ya exista en el proyecto; esto es una referencia conceptual, no un esquema cerrado.

### Tiempo real

- Cualquier cambio en una comanda (nueva línea, línea lista, comanda cerrada) debe reflejarse sin recargar en las pantallas afectadas (POS, Cocina, Camarero) mediante SignalR o el mecanismo de tiempo real que ya use el proyecto.

### Responsive

- Todas las pantallas deben ser usables en móvil, pero **Cocina** y **Mapa de mesas** están optimizadas primero para pantalla grande/tablet; **Camarero** está optimizada primero para móvil.
- Nada de layouts fijos en píxeles; usar unidades relativas (`rem`, `%`, `fr`, `minmax()`) y Flexbox/Grid.

### Estilo de trabajo esperado de Claude Code en este proyecto

- Antes de crear un componente nuevo, revisar si ya existe una estructura CSS base reutilizable (`base.css`) y extenderla, no duplicar estilos.
- No introducir ninguna dependencia de UI externa (Bootstrap, MudBlazor, Radzen, etc.) salvo que se pida explícitamente.
- Mantener la lógica de negocio (apertura de mesa, fusión de mesas, bloqueo por reserva, cálculo de facturación) en servicios C# testeables, no en el code-behind de los componentes Blazor.
- Priorizar procedimientos almacenados para operaciones agregadas (cálculo de totales por día/mes, cierre de comanda con validaciones) cuando tenga sentido por rendimiento o atomicidad.

### Servidor de desarrollo

- Mantener siempre el servidor de desarrollo corriendo en segundo plano en `http://localhost:5209`. No apagarlo al terminar una tarea salvo que el usuario lo pida explícitamente.
- Después de cualquier edit a código C#/Razor (Blazor Server no hace hot-reload de C#/Razor con `dotnet run`), matar el proceso anterior y volver a lanzarlo para que el cambio se vea reflejado:

  ```bash
  pkill -f "dotnet run --project src/PosTpv.Web"
  export PATH="$PATH:/root/.dotnet"
  cd /root/pos-tpv
  export Database__Provider=Sqlite
  export ConnectionStrings__Default="Data Source=postpv-dev.db"
  export ASPNETCORE_ENVIRONMENT=Development
  export ASPNETCORE_URLS="http://localhost:5209"
  export DOTNET_gcServer=0
  export DOTNET_GCHeapHardLimit=0x10000000
  nohup dotnet run --project src/PosTpv.Web > /tmp/dotnet-run.log 2>&1 &
  disown
  ```
- `dotnet` no está en el PATH por defecto en este entorno; está en `/root/.dotnet/dotnet`.
- Se usa el proveedor Sqlite dev (`Database__Provider=Sqlite`) porque no hay SQL Server/Docker disponible en este entorno; el `GCHeapHardLimit` evita el fallo `GC heap initialization failed` que ocurre con la configuración de memoria por defecto en este contenedor.
- Cambios solo de CSS (`*.razor.css`, `app.css`) sí se reflejan sin reiniciar en muchos casos, pero ante la duda reiniciar igual.
