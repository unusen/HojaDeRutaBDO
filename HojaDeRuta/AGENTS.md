# Guía para agentes

## Alcance del proyecto

- La solución es una aplicación ASP.NET Core 8 (`HojaDeRuta.sln`), con EF Core y SQL Server.
- El código principal está en `HojaDeRuta/`; los scripts y recursos de base de datos están en `HojaDeRuta/SqlScripts` y `HojaDeRuta/Database`.
- Antes de modificar código, revisar los patrones del módulo afectado y reutilizar los servicios, modelos y convenciones existentes.

## Dominio funcional

- La aplicación controla el circuito de una hoja de ruta asociada a documentos de trabajo: creación, revisión, aprobación/rechazo, firma y notificaciones.
- Los estados globales son `Pendiente`, `Aprobada` y `Rechazada`. Una hoja aprobada o rechazada no debe poder editarse como una pendiente.
- El número de hoja se genera automáticamente y el identificador se compone de sector + número. El preparador corresponde al usuario autenticado que crea la hoja.
- El flujo de revisión es jerárquico y ascendente: `Reviso`, `Gerente/Dir.`, `Engagement Partner` y `Socio firmante`. Solo el responsable de la etapa activa puede aprobar, rechazar o firmar.
- Un rechazo detiene el circuito, conserva el motivo y notifica al preparador. No debe permitir que continúe la aprobación normal.
- La firma final corresponde únicamente al socio firmante asignado, una vez alcanzada su etapa y satisfechas las validaciones previas.

## Reglas que no deben regresionar

- Para crear o guardar una hoja se requieren los campos obligatorios, un socio firmante, una cadena de revisores válida y un documento principal válido.
- Los revisores deben respetar jerarquía ascendente respecto del preparador y de la etapa anterior. Si existe gestor final, debe ser válido.
- El documento principal debe existir en la ruta configurada o estar correctamente adjuntado, según el modo de trabajo. Su ausencia bloquea la firma.
- Cuando el tipo de documento es `Informe del auditor`, la auditoría debe estar completa antes de firmar y cumplir `Activo = Pasivo + Patrimonio Neto`.
- La auditoría puede modificarse antes de la aprobación, pero queda en solo lectura una vez firmada la hoja.
- Una transición de etapa debe actualizar el responsable actual y programar la notificación correspondiente: primer revisor al crear, siguiente revisor al aprobar, preparador al rechazar y gestor final al firmar.
- Los permisos de acceso a una hoja, a su auditoría y a las acciones del flujo se deben validar del lado servidor; no basarse únicamente en botones u ocultamientos de la interfaz.

## Forma de trabajo

- Mantener los cambios acotados a la necesidad solicitada. No reformatear ni modificar archivos no relacionados.
- No sobrescribir cambios preexistentes del usuario ni usar operaciones destructivas de Git sin autorización explícita.
- No agregar secretos, cadenas de conexión, tokens ni datos productivos al repositorio. `appsettings.json` y `appsettings.Development.json` están excluidos intencionalmente.
- Preferir cambios pequeños y fáciles de revisar. Explicar supuestos y riesgos cuando no puedan validarse desde el repositorio.
- Conservar el idioma y estilo predominantes del archivo o módulo que se esté editando.

## Base de datos y SQL Server

- Todas las consultas SQL que se envíen para ejecutar en el servidor deben estar en **una única línea**, ya que allí se ejecutan mediante `sqlcmd`.
- Evitar separadores de lote como `GO` en esas consultas. Si se requiere más de una sentencia, confirmar primero que el mecanismo de ejecución las admite.
- Antes de proponer `UPDATE`, `DELETE`, `ALTER` o cualquier operación con impacto en datos, validar el alcance con una consulta `SELECT` equivalente y comunicar claramente el efecto esperado.
- No ejecutar cambios destructivos ni de datos productivos salvo pedido explícito del usuario.
- Mantener consistencia entre el modelo de EF Core, el `DbContext` y los scripts SQL cuando el cambio afecte el esquema.
- Si se solicita la modificacion de un stored procedure, pedir confirmacion para modificarlo tambien en el archivo del repo ubicado en ...Database/StoredProcedure
- Solo esta ultima modificacion debe mantener el formateo sql correcto, en lugar de hacerse en una sola linea

## Implementación .NET

- Respetar nullable reference types y evitar suprimir advertencias sin una justificación concreta.
- Favorecer operaciones asíncronas y pasar `CancellationToken` cuando el patrón existente lo contemple.
- No introducir dependencias nuevas si la solución ya ofrece una alternativa adecuada.
- Para funcionalidades visibles, revisar también controladores, servicios, vistas y modelos involucrados; no limitarse a la primera capa encontrada.
- Al modificar estados, permisos, adjuntos, auditoría o notificaciones, revisar los efectos sobre todo el flujo y los mensajes de error que recibe el usuario.

## Verificación y entrega

- Ejecutar la verificación más específica disponible para el cambio. Como mínimo, cuando sea viable: `dotnet build HojaDeRuta.sln`.
- Si hay pruebas relevantes, ejecutarlas; no afirmar que algo fue probado si no se ejecutó.
- Para cambios del flujo, cubrir según corresponda: alta válida e inválida, jerarquía de revisores, autorización del actor actual, rechazo, firma, integridad del adjunto, auditoría de `Informe del auditor` y programación/reintento de notificaciones.
- Informar en la entrega: qué se cambió, cómo se verificó y cualquier limitación o paso pendiente.
