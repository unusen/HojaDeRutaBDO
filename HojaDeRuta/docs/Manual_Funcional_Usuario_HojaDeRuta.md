# Manual Funcional para Usuario Final

## Hoja de Ruta

Version inicial orientada a usuario final.

Importante:
- Este manual describe el comportamiento funcional implementado actualmente en la aplicacion.
- Las imagenes incluidas son mockups ilustrativos basados en la interfaz actual. No reemplazan futuras capturas reales.
- Algunas acciones dependen del rol del usuario y de la etapa activa de la hoja.

## 1. Introduccion

La aplicacion **Hoja de Ruta** permite registrar, revisar, aprobar, rechazar y firmar hojas asociadas a documentos de trabajo. Su objetivo es ordenar el circuito de control interno y dejar trazabilidad sobre:
- quien preparo la hoja
- quien debe revisarla
- en que estado se encuentra
- que documento fisico o archivo digital la respalda
- que notificaciones fueron enviadas

Esta guia esta pensada para usuarios finales. Por eso explica:
- que ve el usuario en cada pantalla
- que campos debe completar
- que validaciones realiza el sistema
- que hacer cuando aparece un error o un bloqueo

## 2. Acceso al sistema

### 2.1 Ingreso

El acceso se realiza con la cuenta corporativa autenticada de la organizacion. Al ingresar correctamente, el usuario ve:
- el nombre de la aplicacion en el encabezado
- su nombre de usuario en la parte superior derecha
- el boton para cerrar sesion
- el acceso a la pantalla principal

### 2.2 Cierre de sesion

Para salir de la aplicacion:
1. Ubique el icono de salida en el encabezado superior derecho.
2. Presionelo una sola vez.
3. El sistema mostrara un aviso breve de cierre de sesion y completara la salida.

### 2.3 Errores de acceso posibles

`Acceso denegado a la aplicacion`
- Ocurre cuando el usuario no pertenece a los grupos habilitados.
- Accion recomendada: solicitar revision de permisos al administrador.

`Tu usuario no tiene permiso para visualizar esta hoja`
- Ocurre cuando el usuario intenta abrir una hoja fuera de su alcance.
- Accion recomendada: verificar si participa del flujo o pedir acceso al responsable.

`No se pudo identificar su sesion de usuario`
- Ocurre si la sesion expiro o llego incompleta.
- Accion recomendada: volver a ingresar al sistema.

## 3. Pantalla principal: listado de hojas

La pantalla principal muestra el listado de hojas disponibles para el usuario.

### 3.1 Acciones principales

En la parte superior se encuentran:
- `Crear Hoja`: inicia el alta de una nueva hoja.
- `Generar Reportes`: acceso secundario para reportes. No es el foco de este manual.

### 3.2 Filtros disponibles

El panel `Filtrar hojas de ruta` permite buscar mas rapido.

Se puede filtrar por:
- numero de hoja
- cliente
- estado
- sector
- socio firmante
- fecha desde
- fecha hasta

Ademas, la pantalla permite:
- definir registros por pagina
- ir a una pagina especifica
- limpiar filtros
- alternar entre `Mostrar pendientes` y `Mostrar todas`

### 3.3 Estados visibles

Cada hoja puede verse con uno de estos estados:

`Pendiente`
- La hoja aun esta en revision o esperando firma.

`Aprobada`
- La hoja completo el circuito y ya fue firmada.

`Rechazada`
- La hoja fue detenida por un rechazo en alguna etapa.

### 3.4 Acciones por registro

En cada fila aparecen iconos de accion:
- `Ver detalle`: abre la hoja en modo consulta.
- `Editar`: solo se habilita si la hoja sigue pendiente.

Si la hoja esta `Aprobada` o `Rechazada`, el icono de edicion aparece deshabilitado.

### 3.5 Situaciones frecuentes en esta pantalla

`No encontramos hojas de ruta para los filtros seleccionados`
- Significa que no hay resultados con los criterios actuales.
- Accion recomendada: limpiar filtros y volver a buscar.

`No pudimos actualizar la lista en este momento`
- Ocurre si falla la carga del listado.
- Accion recomendada: reintentar luego de unos segundos.

## 4. Creacion de una hoja de ruta

### 4.1 Paso a paso

1. Ingrese a `Crear Hoja`.
2. Complete los datos generales.
3. Defina los responsables del flujo de revision.
4. Informe las rutas de trabajo y adjunte el documento principal.
5. Presione `Crear hoja`.

Si todo es correcto:
- la hoja se guarda
- nace con estado `Pendiente`
- el sistema genera los estados del flujo
- se programa la notificacion al primer revisor

### 4.2 Campos obligatorios

Antes de crear la hoja, deben completarse estos datos:
- Cliente
- Sector
- Subarea
- Tipo de Documento
- Descripcion
- Contrato Plataforma
- Socio firmante
- Lugar de firma
- Ruta de papeles
- Ruta del documento
- Documento adjunto principal

### 4.3 Reglas funcionales importantes

`Preparador`
- El campo `Preparo` se toma del usuario logueado.
- En la creacion, debe coincidir con quien esta generando la hoja.

`Numero e identificador`
- El numero se genera automaticamente.
- El identificador final de la hoja se arma con sector + numero.

`Estado inicial`
- Toda hoja creada queda en estado `Pendiente`.

`Edicion`
- La hoja puede modificarse mientras no este aprobada ni rechazada.

`Hoja rechazada`
- Si una hoja fue rechazada, deja de seguir el circuito normal.
- El motivo de rechazo queda visible para consulta.

### 4.4 Validaciones del flujo de revisores

El sistema valida la configuracion de responsables antes de guardar.

Reglas principales:
- la hoja debe tener preparador
- debe existir un socio firmante
- si se informa un gestor final, debe ser valido
- la cadena de revision debe respetar jerarquia ascendente

Eso significa que:
- `Reviso` debe tener un nivel superior al preparador
- `Gerente/Dir.` debe tener un nivel superior a `Reviso`
- `Engagement Partner` debe tener un nivel superior a la etapa anterior

Si esto no se cumple, el sistema no deja continuar.

### 4.5 Validaciones del adjunto

La hoja necesita un archivo principal.

Si la organizacion trabaja con carpeta compartida:
- el archivo debe existir fisicamente en la ruta indicada
- si la carpeta no existe o no se puede acceder, la validacion falla

Si la organizacion trabaja con archivo cargado en la aplicacion:
- el archivo debe haber sido adjuntado
- el archivo no debe faltar
- el archivo no debe haberse alterado o dañado

### 4.6 Errores posibles al crear

`Revisa los campos obligatorios antes de continuar`
- Faltan datos obligatorios.

`Revisa la configuracion de revisores antes de continuar`
- Hay una inconsistencia en el flujo de aprobacion.

`No pudimos procesar la solicitud`
- Ocurrio un error general de procesamiento.

## 5. Edicion de una hoja pendiente

### 5.1 Cuando se puede editar

La opcion `Guardar Cambios` aparece solamente en hojas pendientes.

No se puede editar normalmente una hoja:
- aprobada
- rechazada

### 5.2 Paso a paso

1. Desde el listado, abra la hoja.
2. Si ingreso en modo consulta, presione `Modificar`.
3. Realice los cambios necesarios.
4. Presione `Guardar Cambios`.

### 5.3 Que valida el sistema al guardar cambios

Se vuelven a controlar:
- campos obligatorios
- consistencia de revisores
- integridad del adjunto principal
- sincronizacion del flujo de estados

### 5.4 Resultado esperado

Si la modificacion es correcta:
- se guardan los cambios
- se regeneran los estados del flujo si corresponde
- se actualiza el responsable actual de la hoja

### 5.5 Errores posibles al editar

`No pudimos guardar los cambios de la hoja`
- Error al actualizar informacion.

`Revisa los campos obligatorios antes de continuar`
- Falta informacion necesaria.

`Revisa la configuracion de revisores antes de continuar`
- El flujo definido ya no es valido.

## 6. Flujo de revision y aprobaciones

La hoja puede pasar por estas etapas:
- `Reviso`
- `Gerente/Dir.`
- `Engagement Partner`
- `Socio firmante`

### 6.1 Regla central

Solo el responsable de la etapa actual puede actuar sobre la hoja.

Si el usuario no es el actor de la etapa vigente:
- no vera acciones habilitadas
- o recibira un mensaje indicando que no puede actuar

### 6.2 Botones posibles

Segun la etapa y el perfil del usuario pueden aparecer:
- `Aprobar`
- `Rechazar`
- `Firmar Documento`

Reglas:
- `Aprobar` aparece en etapas intermedias
- `Rechazar` aparece cuando la etapa actual esta habilitada para el usuario
- `Firmar Documento` aparece solo en la etapa final del socio firmante

### 6.3 Aprobacion de una etapa

Paso a paso:
1. Abra la hoja.
2. Verifique que sea la etapa actual bajo su responsabilidad.
3. Presione `Aprobar`.
4. Confirme la accion.

Que ocurre luego:
- la etapa actual pasa a `Aprobada`
- la hoja avanza al siguiente responsable
- se programa la notificacion al proximo revisor
- si el proximo revisor pertenece a otra area, puede programarse una solicitud de acceso cruzado

### 6.4 Rechazo de una etapa

Paso a paso:
1. Abra la hoja.
2. Presione `Rechazar`.
3. Ingrese el motivo del rechazo.
4. Confirme la accion.

Que ocurre luego:
- la etapa actual queda rechazada
- la hoja completa pasa a estado `Rechazada`
- el preparador recibe la notificacion de rechazo
- el motivo queda visible dentro de la hoja


### 6.5 Bloqueos posibles en revision

`La hoja ya no esta disponible`
- La hoja fue eliminada o no se pudo cargar.

`La hoja no tiene una etapa activa para procesar`
- No hay una etapa pendiente valida en ese momento.

`Tu usuario no puede actuar sobre la etapa actual`
- Otro responsable debe intervenir.

`La hoja cambio de responsable y ya no puede procesarse desde esta pantalla`
- Debe recargarse la hoja para ver la situacion actual.

`La hoja fue rechazada`
- Ya no puede seguir el circuito de aprobacion normal.

## 7. Firma final

La firma final solo puede completarla el `Socio firmante` asignado.

### 7.1 Condiciones previas para firmar

Antes de permitir la firma, el sistema valida:
- que la hoja exista
- que la hoja no este rechazada
- que el usuario actual sea el socio firmante asignado
- que el usuario pueda actuar en la etapa actual
- que el archivo principal exista y sea valido
- que la auditoria este completa si corresponde
- que exista un gestor final valido

### 7.2 Paso a paso

1. Abra la hoja en la etapa final.
2. Verifique que aparezca `Firmar Documento`.
3. Si corresponde, cargue o confirme el archivo principal.
4. Presione `Firmar Documento`.
5. Confirme la accion.

### 7.3 Que hace el sistema al firmar

Si todo es correcto:
- valida permisos de firma
- valida auditoria cuando aplica
- copia o finaliza el documento firmado
- cierra las etapas pendientes desde la firma
- marca la hoja como `Aprobada`
- programa la notificacion al gestor final

### 7.4 Errores frecuentes de firma

`Solo el socio firmante asignado puede completar esta firma`
- El usuario no coincide con el firmante definido.

`Tu usuario no puede completar la firma en la etapa actual`
- La hoja aun no llego a la firma o no corresponde al usuario.

`No encontramos el archivo adjunto en la carpeta indicada`
- El sistema no encuentra el documento fisico.

`Antes de firmar, completa toda la informacion de auditoria requerida`
- Aplica a hojas de tipo `Informe del auditor`.

`No pudimos completar la firma porque falta informacion del gestor final`
- Debe revisarse el dato antes de reintentar.

`No se pudo guardar el archivo final en el destino configurado`
- Error al copiar o finalizar el documento.

## 8. Auditoria asociada

La auditoria asociada aplica solo cuando el tipo de documento es `Informe del auditor`.

### 8.1 Advertencia visible

Cuando la hoja requiere auditoria, puede aparecer una advertencia como esta:
- la hoja puede avanzar de etapa
- pero el socio firmante no podra firmar hasta completar la carga

### 8.2 Datos a completar

El modal de auditoria solicita:
- Activo
- Pasivo
- Patrimonio Neto
- Moneda
- Tipo Numeracion
- Resultado
- Total Ingresos
- Total Otros Ingresos

### 8.3 Validaciones de auditoria

Para poder considerarse completa:
- todos los campos deben estar informados
- la moneda debe estar seleccionada
- el tipo de numeracion debe estar seleccionado
- debe cumplirse `Activo = Pasivo + Patrimonio Neto`

### 8.4 Restricciones

La auditoria:
- puede cargarse o actualizarse mientras la hoja no este aprobada
- queda solo lectura cuando la hoja ya fue firmada

### 8.5 Errores posibles

`Tu usuario no tiene permisos para acceder a la auditoria`
- El usuario no esta autorizado para esa hoja.

`No encontramos la hoja asociada a la auditoria`
- La hoja no existe o no esta accesible.

`No pudimos guardar la informacion de auditoria`
- Error general al grabar los datos.

## 9. Adjuntos, alertas y validaciones visibles

La hoja puede mostrar avisos importantes en la parte superior.

### 9.1 Error critico de archivo

Si el sistema no encuentra el archivo principal, mostrara un aviso de error y permitira `Verificar ahora`.

Consecuencias:
- la firma queda bloqueada
- el usuario debe corregir la ruta o el archivo

### 9.2 Advertencia por multiples archivos

Si en la carpeta hay varios archivos, el sistema puede advertir que verifique cual es el correcto.

Consecuencias:
- no siempre bloquea el avance
- pero requiere validacion manual del usuario

### 9.3 Validacion de obligatorios en pantalla

Cuando el usuario intenta crear o guardar una hoja, el sistema puede listar faltantes como:
- falta completar un campo obligatorio
- falta adjuntar el documento fisico

## 10. Notificaciones

La hoja dispone de un panel de seguimiento de notificaciones.

### 10.1 Quien recibe cada notificacion

`Al crear la hoja`
- el primer revisor del flujo

`Al aprobar una etapa`
- el siguiente revisor habilitado

`Al rechazar una etapa`
- el preparador de la hoja

`Al firmar la hoja`
- el gestor final

`Si la revision corresponde a otra area`
- puede enviarse una solicitud de acceso cruzado

### 10.2 Que informacion contiene el correo

Las notificaciones pueden incluir:
- numero de hoja
- cliente
- sector
- ruta de papeles
- ruta del documento
- acceso directo a la hoja
- motivo de rechazo, cuando aplica

### 10.3 Estados del panel de notificaciones

`Pendiente`
- La notificacion fue programada y espera procesamiento.

`Procesando`
- El envio esta en curso.

`Enviada` o `Completada`
- El correo fue despachado correctamente.

`Fallida`
- El correo no pudo enviarse.

### 10.4 Reintento

Si una notificacion fallo y el usuario esta autorizado:
- puede reintentarse desde la hoja
- el sistema vuelve a programar el envio

### 10.5 Mensajes amigables esperables

`No pudimos entregar el email porque la direccion del destinatario no existe o no esta habilitada`
- El destinatario informado no es valido.

`No pudimos entregar el email a uno o mas destinatarios`
- Fallaron uno o varios correos de destino.

`El servidor de correo tardo demasiado en responder`
- Reintentar mas tarde.

`No pudimos enviar el email porque el servicio de directorio no respondio`
- Puede ser un problema temporal de resolucion o directorio.

`No pudimos conectarnos con el servidor de correo`
- Puede tratarse de un problema de red o disponibilidad del servicio.

## 11. Errores y como proceder

### 11.1 No veo una hoja que esperaba encontrar

Posibles causas:
- hay filtros activos
- la hoja no esta dentro de sus pendientes
- el usuario no tiene permiso

Que hacer:
1. Limpiar filtros.
2. Cambiar a `Mostrar todas`.
3. Verificar permisos con el responsable.

### 11.2 No me deja editar una hoja

Posibles causas:
- la hoja ya fue aprobada
- la hoja fue rechazada
- no ingreso en modo modificacion

Que hacer:
1. Abrir la hoja en detalle.
2. Verificar si aparece `Modificar`.
3. Confirmar que el estado siga pendiente.

### 11.3 No puedo aprobar o rechazar

Posibles causas:
- no es el responsable actual
- la hoja cambio de etapa
- la hoja ya fue rechazada

Que hacer:
1. Recargar la hoja.
2. Verificar la etapa resaltada.
3. Confirmar que la accion corresponda a su usuario.

### 11.4 No aparece el boton de firmar

Posibles causas:
- la hoja no esta en etapa final
- el usuario no es el socio firmante
- existe un bloqueo previo

Que hacer:
1. Verificar que la etapa actual sea `Socio firmante`.
2. Confirmar que el firmante asignado sea el usuario actual.
3. Revisar alertas de archivo y auditoria.

### 11.5 El sistema indica que falta el archivo fisico

Posibles causas:
- la ruta del documento no existe
- el nombre del archivo no coincide
- el archivo fue movido o eliminado

Que hacer:
1. Revisar la ruta informada.
2. Confirmar el nombre del archivo.
3. Usar `Verificar ahora`.

### 11.6 El documento requiere auditoria y no deja firmar

Que hacer:
1. Abrir la auditoria.
2. Completar todos los campos.
3. Verificar que `Activo = Pasivo + Patrimonio Neto`.
4. Guardar y volver a intentar la firma.

### 11.7 Me aparece acceso denegado

Posibles causas:
- la hoja no pertenece a su flujo
- el usuario no tiene rol suficiente

Que hacer:
1. Confirmar con el responsable de la hoja.
2. Solicitar revision de permisos.

### 11.8 La notificacion no salio

Que hacer:
1. Abrir el panel `Notificaciones`.
2. Revisar el estado fallido.
3. Reintentar si la opcion esta disponible.
4. Si vuelve a fallar, revisar destinatarios o esperar unos minutos.

### 11.9 El adjunto no abre o no descarga

Posibles causas:
- el archivo no existe
- el usuario no tiene permiso
- hubo un problema temporal de acceso

Que hacer:
1. Verificar permisos.
2. Validar la existencia del archivo.
3. Reintentar desde la hoja.

## 12. Casos de prueba recomendados para validar este manual

- Crear una hoja completa con datos validos y adjunto valido.
- Intentar crear una hoja con campos obligatorios vacios.
- Definir revisores con jerarquia invalida.
- Aprobar una etapa con el usuario correcto.
- Intentar aprobar una etapa con un usuario que no es el responsable actual.
- Rechazar una hoja informando motivo.
- Intentar firmar una hoja rechazada.
- Intentar firmar una hoja sin archivo valido.
- Intentar firmar un `Informe del auditor` con auditoria incompleta.
- Completar auditoria correctamente y luego firmar.
- Verificar el panel de notificaciones en estados pendiente, procesando, enviada y fallida.
- Reintentar una notificacion fallida.