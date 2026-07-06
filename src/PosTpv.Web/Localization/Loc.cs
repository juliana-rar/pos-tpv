using System.Globalization;

namespace PosTpv.Web.Localization;

/// <summary>
/// Tiny English-keyed localizer. UI code is written in English and each visible
/// string is wrapped with <see cref="T"/>; when the current UI culture is Spanish
/// the matching translation is returned, otherwise the English literal is used as
/// a safe fallback. This keeps translation friction low — no resource files, no
/// invented keys — while covering the whole application.
///
/// The active culture is set per request/circuit by the RequestLocalization
/// middleware (cookie-driven), so <see cref="CultureInfo.CurrentUICulture"/> is the
/// single source of truth here.
/// </summary>
public static class Loc
{
    public const string CookieName = "pos-culture";
    public static readonly string[] Supported = { "en", "es" };

    public static bool IsSpanish =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("es", StringComparison.OrdinalIgnoreCase);

    /// <summary>Translate an English string to the current UI culture.</summary>
    public static string T(string english)
    {
        if (IsSpanish && _es.TryGetValue(english, out var es))
            return es;
        return english;
    }

    // English → Spanish. Anything not present falls back to the English literal.
    private static readonly Dictionary<string, string> _es = new(StringComparer.Ordinal)
    {
        // ---- Navigation / chrome ----
        ["Home"] = "Inicio",
        ["Dashboard"] = "Panel",
        ["Point of Sale"] = "Punto de venta",
        ["Orders"] = "Pedidos",
        ["Tables"] = "Mesas",
        ["Reservations"] = "Reservas",
        ["Kitchen"] = "Cocina",
        ["Billing"] = "Facturación",
        ["Management"] = "Gestión",
        ["Products"] = "Productos",
        ["Categories"] = "Categorías",
        ["Menu"] = "Menú",
        ["Sign out"] = "Cerrar sesión",
        ["Toggle theme"] = "Cambiar tema",
        ["Language"] = "Idioma",
        ["Access denied"] = "Acceso denegado",
        ["Your role does not have permission to view this page."] = "Tu rol no tiene permiso para ver esta página.",
        ["Back to home"] = "Volver al inicio",

        // ---- Launcher / hub ----
        ["Welcome back"] = "Bienvenido de nuevo",
        ["Choose a module to get started"] = "Elige un módulo para empezar",
        ["Live sales overview and top products"] = "Resumen de ventas en vivo y productos más vendidos",
        ["Take orders and charge tables"] = "Toma pedidos y cobra mesas",
        ["Track and charge open orders"] = "Sigue y cobra los pedidos abiertos",
        ["Manage the floor plan and joins"] = "Gestiona el plano de sala y las uniones",
        ["Book and manage table reservations"] = "Reserva y gestiona las mesas",
        ["Floor plan, joins and reservations"] = "Plano de sala, uniones y reservas",
        ["Kitchen display for live tickets"] = "Pantalla de cocina para comandas en vivo",
        ["Revenue reports and invoices"] = "Informes de ingresos y facturas",
        ["Manage the product catalogue"] = "Gestiona el catálogo de productos",
        ["Organize products into categories"] = "Organiza los productos en categorías",
        ["Enter"] = "Entrar",

        // ---- Dashboard ----
        ["Sales today"] = "Ventas de hoy",
        ["Sales this month"] = "Ventas del mes",
        ["Orders today"] = "Pedidos de hoy",
        ["Open orders"] = "Pedidos abiertos",
        ["Occupied tables"] = "Mesas ocupadas",
        ["Reservations today"] = "Reservas de hoy",
        ["Top products"] = "Productos más vendidos",
        ["No sales recorded yet."] = "Aún no hay ventas registradas.",
        ["Product"] = "Producto",
        ["Qty"] = "Cant.",
        ["Revenue"] = "Ingresos",
        ["Loading…"] = "Cargando…",

        // ---- Login ----
        ["Sign in to your point of sale"] = "Inicia sesión en tu punto de venta",
        ["Username"] = "Usuario",
        ["PIN / Password"] = "PIN / Contraseña",
        ["Sign in"] = "Iniciar sesión",
        ["Invalid username or PIN."] = "Usuario o PIN no válidos.",
        ["Demo accounts (PIN"] = "Cuentas demo (PIN",

        // ---- POS ----
        ["Order"] = "Pedido",
        ["No open table."] = "No hay mesa abierta.",
        ["Select a table"] = "Seleccionar mesa",
        ["Unavailable"] = "No disponible",
        ["No products in this category."] = "No hay productos en esta categoría.",
        ["Tap a product to add it."] = "Toca un producto para añadirlo.",
        ["VAT"] = "IVA",
        ["Send to kitchen"] = "Enviar a cocina",
        ["Cash"] = "Efectivo",
        ["Card"] = "Tarjeta",
        ["Voucher"] = "Vale",
        ["Other"] = "Otro",
        ["Split"] = "Dividir",
        ["Open a table"] = "Abrir mesa",
        ["seats"] = "plazas",
        ["Base price"] = "Precio base",
        ["Quantity"] = "Cantidad",
        ["Extras"] = "Extras",
        ["Loading extras…"] = "Cargando extras…",
        ["No extras available for this product."] = "Este producto no tiene extras disponibles.",
        ["Comment"] = "Comentario",
        ["No onion, well done…"] = "Sin cebolla, muy hecho…",
        ["No onion, well done, extra sauce…"] = "Sin cebolla, muy hecho, salsa extra…",
        ["Cancel"] = "Cancelar",
        ["Add to order"] = "Añadir al pedido",
        ["Payment"] = "Pago",
        ["Total due"] = "Total a pagar",
        ["Split equally"] = "Dividir en partes iguales",
        ["Apply"] = "Aplicar",
        ["Remove"] = "Quitar",
        ["Add"] = "Añadir",
        ["Rest in cash"] = "Resto en efectivo",
        ["Rest in card"] = "Resto en tarjeta",
        ["Remaining"] = "Restante",
        ["Change"] = "Cambio",
        ["Fully paid"] = "Pagado completo",
        ["Tendered"] = "Entregado",
        ["Finalize"] = "Finalizar",
        ["Keyboard shortcuts"] = "Atajos de teclado",
        ["Select category"] = "Seleccionar categoría",
        ["Quantity of last line"] = "Cantidad de la última línea",
        ["Remove last line"] = "Quitar última línea",
        ["Open table selector"] = "Abrir selector de mesas",
        ["Send to kitchen (shortcut)"] = "Enviar a cocina",
        ["Charge (cash)"] = "Cobrar (efectivo)",
        ["Split payment"] = "Dividir pago",
        ["Finalize payment (in dialog)"] = "Finalizar pago (en diálogo)",
        ["Close dialog"] = "Cerrar diálogo",
        ["Toggle this help"] = "Mostrar/ocultar esta ayuda",
        ["Line comment"] = "Comentario de línea",
        ["Note for the kitchen"] = "Nota para la cocina",
        ["Save"] = "Guardar",
        ["Comment (title)"] = "Comentario",
        ["Remove (title)"] = "Quitar",
        // Comment presets
        ["No onion"] = "Sin cebolla",
        ["Well done"] = "Muy hecho",
        ["No cheese"] = "Sin queso",
        ["Extra sauce"] = "Salsa extra",
        ["Spicy"] = "Picante",
        ["Gluten free"] = "Sin gluten",
        // Order / item status labels
        ["Open"] = "Abierto",
        ["Sent"] = "Enviado",
        ["Cooking"] = "Cocinando",
        ["Preparing"] = "Preparando",
        ["Ready"] = "Listo",
        ["Delivered"] = "Entregado",
        ["Paid"] = "Pagado",
        ["Table"] = "Mesa",

        // ---- Orders ----
        ["All statuses"] = "Todos los estados",
        ["Refresh"] = "Actualizar",
        ["items"] = "artículos",
        ["No open orders."] = "No hay pedidos abiertos.",
        ["InPreparation"] = "En preparación",
        ["Food"] = "Comida",
        ["Drinks"] = "Bebidas",
        ["First course"] = "Primer plato",
        ["Second course"] = "Segundo plato",
        ["Desserts"] = "Postres",
        ["Course"] = "Plato",
        ["Serve"] = "Servir",
        ["Serve drinks"] = "Servir bebidas",
        ["Served"] = "Servida",
        ["Fire firsts"] = "Marchar primeros",
        ["Firsts fired"] = "Primeros marchados",
        ["Tell the kitchen to start the first courses"] = "Avisa a cocina para empezar los primeros platos",
        ["Fire seconds"] = "Marchar segundos",
        ["Seconds fired"] = "Segundos marchados",
        ["Tell the kitchen to start the second courses"] = "Avisa a cocina para empezar los segundos platos",
        ["Fire first courses"] = "Marchar primeros",
        ["Charge"] = "Cobrar",
        ["Edit order"] = "Editar comanda",
        ["Current lines"] = "Líneas actuales",
        ["Done"] = "Hecho",
        ["Send to kitchen and close"] = "Enviar a cocina y cerrar",
        ["Confirm and close"] = "Confirmar y cerrar",
        ["or split"] = "o dividir",

        // ---- Tables ----
        ["Floor plan"] = "Plano de sala",
        ["Free"] = "Libre",
        ["Occupied"] = "Ocupada",
        ["Reserved"] = "Reservada",
        ["Save layout"] = "Guardar distribución",
        ["selected"] = "seleccionadas",
        ["Join"] = "Unir",
        ["New table"] = "Nueva mesa",
        ["Join tables"] = "Unir mesas",
        ["Edit layout"] = "Editar distribución",
        ["Drag to move · corner handle to resize · top handle to rotate · ◻ to change shape · 🔒 to lock. Changes are saved when you press"] =
            "Arrastra para mover · tirador de esquina para redimensionar · tirador superior para rotar · ◻ para cambiar la forma · 🔒 para bloquear. Los cambios se guardan al pulsar",
        ["Change shape"] = "Cambiar forma",
        ["Tap two or more free tables to join them into one group that shares a single bill."] =
            "Toca dos o más mesas libres para unirlas en un grupo que comparte una sola cuenta.",
        ["Edit table"] = "Editar mesa",
        ["Name"] = "Nombre",
        ["Seats"] = "Plazas",
        ["Shape"] = "Forma",
        ["Joined"] = "Unida",
        ["Shares a bill with:"] = "Comparte cuenta con:",
        ["Delete"] = "Eliminar",
        ["Separate"] = "Separar",
        ["No tables yet. Create one to start."] = "Aún no hay mesas. Crea una para empezar.",
        ["Lock / unlock"] = "Bloquear / desbloquear",
        ["Rotate"] = "Rotar",
        ["Resize"] = "Redimensionar",
        ["tables joined."] = "mesas unidas.",
        // Table shapes
        ["Square"] = "Cuadrada",
        ["Round"] = "Redonda",
        ["Rectangular"] = "Rectangular",
        ["Oval"] = "Ovalada",

        // ---- Kitchen ----
        ["Kitchen display"] = "Pantalla de cocina",
        ["● Live"] = "● En vivo",
        ["● Offline"] = "● Sin conexión",
        ["active"] = "activos",
        ["No orders in the kitchen. Enjoy the calm. ☕"] = "No hay pedidos en cocina. Disfruta la calma. ☕",
        ["Mark all ready"] = "Marcar todo listo",
        ["just now"] = "ahora mismo",
        ["Fire second courses"] = "Marchar segundos",
        ["Got it"] = "Recibido",

        // ---- Billing ----
        ["From"] = "Desde",
        ["To"] = "Hasta",
        ["VAT collected"] = "IVA recaudado",
        ["Invoices"] = "Facturas",
        ["Average ticket"] = "Ticket medio",
        ["Revenue by day"] = "Ingresos por día",
        ["No revenue in this range."] = "Sin ingresos en este rango.",
        ["Payment methods"] = "Métodos de pago",
        ["No payments in this range."] = "Sin pagos en este rango.",
        ["Invoice"] = "Factura",
        ["Method"] = "Método",
        ["Date"] = "Fecha",
        ["No invoices in this range."] = "Sin facturas en este rango.",

        // ---- Products ----
        ["Search…"] = "Buscar…",
        ["New product"] = "Nuevo producto",
        ["Available"] = "Disponible",
        ["Off"] = "Inactivo",
        ["Edit"] = "Editar",
        ["No products found."] = "No se encontraron productos.",
        ["Edit product"] = "Editar producto",
        ["Description"] = "Descripción",
        ["Category"] = "Categoría",
        ["Colour"] = "Color",
        ["Price"] = "Precio",
        ["VAT %"] = "IVA %",
        ["Prep. minutes"] = "Minutos de prep.",
        ["Display order"] = "Orden",
        ["Allergens"] = "Alérgenos",
        ["gluten, dairy…"] = "gluten, lácteos…",
        ["Visible"] = "Visible",
        ["— select —"] = "— seleccionar —",

        // ---- Categories ----
        ["New category"] = "Nueva categoría",
        ["hidden"] = "oculta",
        ["Order:"] = "Orden:",
        ["No categories yet."] = "Aún no hay categorías.",
        ["Edit category"] = "Editar categoría",
        ["Icon (emoji)"] = "Icono (emoji)",
        ["Hidden"] = "Oculta",
        ["Hide"] = "Ocultar",
        ["Show"] = "Mostrar",
        ["Move up"] = "Subir",
        ["Move down"] = "Bajar",
        ["Duplicate"] = "Duplicar",
        ["Move…"] = "Mover…",
        ["Move"] = "Mover",
        ["Move to category"] = "Mover a categoría",
        ["Select a category to manage its products."] = "Selecciona una categoría para gestionar sus productos.",
        ["Cannot delete a category that still has products."] = "No se puede eliminar una categoría que aún tiene productos.",

        // ---- Reservations ----
        ["New reservation"] = "Nueva reserva",
        ["New"] = "Nueva",
        ["Close"] = "Cerrar",
        ["Collapse"] = "Contraer",
        ["Show reservations"] = "Mostrar reservas",
        ["Unassigned"] = "Sin asignar",
        ["Assign"] = "Asignar",
        ["Reassign"] = "Reasignar",
        ["Unassign"] = "Quitar mesa",
        ["Pick a table on the plan…"] = "Elige una mesa en el plano…",
        ["Click a table on the plan to assign the reservation of"] = "Haz clic en una mesa del plano para asignar la reserva de",
        ["no table"] = "sin mesa",
        ["No reservations for this day."] = "No hay reservas para este día.",
        ["Edit reservation"] = "Editar reserva",
        ["Customer name"] = "Nombre del cliente",
        ["Phone"] = "Teléfono",
        ["Party size"] = "Comensales",
        ["Time"] = "Hora",
        ["Duration (min)"] = "Duración (min)",
        ["— none —"] = "— ninguna —",
        ["Status"] = "Estado",
        ["Comments"] = "Comentarios",
        // Reservation status labels
        ["Pending"] = "Pendiente",
        ["Confirmed"] = "Confirmada",
        ["Seated"] = "Sentada",
        ["Finished"] = "Finalizada",
        ["Cancelled"] = "Cancelada",
    };
}
