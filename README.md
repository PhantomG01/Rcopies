# Rcopies

Aplicación Windows x64 para copia, multicopia y espejo. Incluye proyecto completo, EXE portable y `build.bat` para recompilar con .NET SDK 10.

## Inicio y licencia

Descomprime el paquete y abre `portable/Rcopies.exe`. **Abre sin licencia** y permite copia o espejo a **un destino por operación**. Puedes realizar otras operaciones de un destino cuando termine la anterior; no es una cuota de una sola copia para toda la instalación. El límite también se comprueba al intentar incorporar destinos a un lote.

Para multicopia e incorporación de unidades durante el lote importa tu licencia mediante «Licencia». Ese diálogo muestra el identificador de este PC, titular y vencimiento y permite importar archivos `.rcopies-license`. Se conservan las claves e identificadores de la edición anterior: también se aceptan sus archivos `.clonova-license` válidos y se intenta leer la activación que estuviera instalada. No hay servidor ni envío automático de información.

La licencia instalada se guarda en `%LOCALAPPDATA%/Rcopies`. El administrador privado y sus archivos de emisión se entregan por separado, solo al propietario. No son necesarios para utilizar el modo libre.

## Interfaz

- Icono profesional de Rcopies integrado en el EXE y en las ventanas, con recursos para los tamaños habituales de Windows; tema oscuro violeta/fucsia y tu imagen original restaurada como marca de agua discreta.
- Origen arriba; lista única de destinos con iconos, etiquetas, tipo de unidad, sistema de archivos y espacio libre.
- Progreso, velocidad y tiempo restante por destino y global. El tiempo restante es una estimación y cambia según la carga y fase de trabajo.
- Medidor con **RAM usada por Rcopies y RAM libre del equipo**, sin exponer información interna de configuración.
- «Acerca de» conserva los datos de autor y contacto solicitados.

## Destinos y selección

«Unidades» lista unidades locales o extraíbles listas, **sin mínimo de capacidad**. Se mantienen fuera la unidad de Windows, CD/DVD/Blu-ray, unidades de red y RAM. La unidad que contiene el origen queda fuera de los destinos. No se admiten carpetas como destino; para añadir una unidad usa «Unidades» o escribe su raíz, como `E:\`, en «+ Unidad».

Para seleccionar, haz clic en el primer destino y **arrastra el mouse hacia arriba o abajo**, manteniendo pulsado el botón izquierdo. La lista extiende la selección y desplaza las filas al llegar a los bordes. No necesitas pulsar una tecla. **Ctrl + clic** añade o retira destinos separados. Shift + clic sigue disponible como alternativa. El resaltado violeta indica la selección; no hay casillas. «Todas» y «Ninguna» conservan su función.

Cambiar la selección no cancela trabajos ya iniciados. Cada unidad debe tener espacio suficiente para los datos; quitar el mínimo de capacidad no elimina las limitaciones del sistema de archivos ni del espacio libre.

## Copiar

Elige una carpeta, unidad o archivo como origen. Al arrastrar un solo archivo, carpeta o raíz de unidad a Rcopies —incluso sobre la lista de destinos— se toma automáticamente como origen. No se intenta añadir la carpeta como destino. Se conservan los destinos seleccionados, salvo la unidad que contiene el nuevo origen, que se retira para evitar copiar sobre sí misma. Al soltar el origen sobre la lista de destinos, la copia normal comienza automáticamente hacia los destinos seleccionados. Sin selección, solo se establece el origen. Soltar una carpeta en el campo de origen compara automáticamente las carpetas coincidentes; cualquier espejo requiere confirmación antes de cambiar datos. No hay botón Espejo. El origen no puede cambiar durante una operación activa. Puedes añadir grupos de raíces de unidades como destinos arrastrándolos juntos sobre la lista; la selección de varios archivos/carpetas como origen no se admite en esta edición. Selecciona los destinos, elige omitir/sobrescribir/comparar tamaño y fecha y pulsa «Copiar». Por defecto se conserva la carpeta origen y su nombre en cada unidad destino. «Solo contenido» queda desmarcado; puedes activarlo para una copia normal sin carpeta contenedora. Una raíz de unidad se copia por contenido para no crear nombres de carpeta inválidos. La copia no calcula hashes ni relee los archivos para verificarlos. Conserva las comprobaciones de tamaño, fecha, errores de lectura/escritura e identidad del dispositivo.

Los destinos trabajan en paralelo con gestión interna de recursos. «Pausar», «Reanudar» y «Cancelar» actúan sobre las filas seleccionadas. Sin selección, pausa y reanudación actúan sobre todos. «Cancelar todo» detiene el lote y la preparación. Cancelar limpia el progreso retirado sin borrar archivos ya completados, registros ni recuperación del espejo. No hay reanudación por offset después de cerrar la aplicación.

Con licencia puedes añadir unidades al lote mediante «+ Al lote». Una copia incorporada tarde trabaja desde el tramo en curso y recupera lo anterior al terminar el grupo inicial. La detección de unidades nuevas durante una copia permite ignorar, incorporar sin formatear o formatear y copiar. El formateo siempre requiere confirmación adicional, comprobaciones de protección e identidad y elevación; no es automático. Esta incorporación no se ofrece en modo libre ni durante el espejo.

El menú contextual muestra iconos propios para la función auxiliar y la expulsión, con colores acordes a la interfaz.

## Clic derecho: origen auxiliar

Cada copia completada al 100 % puede empezar a ayudar inmediatamente como fuente de lectura, hasta las primeras cuatro fuentes auxiliares. No se espera a que terminen cuatro unidades, no se calcula SHA-256 ni se hace una pasada de lectura adicional para promoverla. Las operaciones con archivos omitidos por la política Omitir no se promueven automáticamente, porque no se ha escrito todo el conjunto. En una fila puedes elegir **«No usar como origen auxiliar»**. La unidad deja de recibir nuevas solicitudes de lectura y no vuelve a promoverse automáticamente mientras siga desactivada. Los lectores ya iniciados terminan el bloque en curso y continúan desde el mismo byte con el siguiente auxiliar disponible. No se reinicia el archivo ni se pierde el progreso; si no hay otro auxiliar válido, se continúa desde el origen principal. Desactivar la función auxiliar no cancela ni pausa la copia que ese propio destino recibe, ni las copias a las que ayudaba. La preparación de promoción puede detenerse, pero no la escritura del destino ni sus lectores beneficiarios.

«Permitir como origen auxiliar» vuelve a habilitar esa preferencia. Si el lote sigue abierto y la copia está completa, se comprueban sus metadatos antes de usarla. La preferencia se conserva en esa fila durante la sesión, incluidos lotes posteriores; no se guarda al cerrar el programa. Los cambios de tamaño o fecha de un auxiliar lo descartan y hacen continuar desde otra fuente. No se detecta una modificación que conserve exactamente ambos metadatos.

## Clic derecho: desconectar unidad

Selecciona varios destinos con Ctrl o Shift y haz clic derecho sobre uno de los seleccionados. **«Desconectar destinos seleccionados (N)»** permite solicitar su expulsión en una operación. Las particiones que pertenecen al mismo dispositivo se agrupan para no solicitar varias veces su retirada. Se muestra un resultado por destino: los que estén en uso, no sean compatibles o reciban un veto de Windows permanecen en la lista. La acción individual sigue disponible al seleccionar una sola fila.

**«Desconectar unidad»** se habilita cuando Windows informa de un dispositivo que admite extracción segura y no hay una operación propia que lo impida. Puede requerir unos instantes de comprobación. Las unidades internas sin capacidad de desconexión no se expulsan.

Antes de solicitar la expulsión se bloquean nuevos lectores auxiliares de la unidad, se cancela su preparación y se espera a que terminen los lectores existentes. Se comprueban otras unidades del mismo dispositivo: no se permite retirar Windows, el origen, la aplicación, sus registros ni un dispositivo con copias o espejos activos. Si hay varias unidades afectadas se muestra una confirmación. Se vuelve a comprobar la identidad antes de pedir la retirada a Windows.

No se fuerza el desmontaje ni se deshabilitan controladores. Windows puede rechazar la solicitud por archivos abiertos o restricciones del dispositivo. Si una copia pausada mantiene un lector auxiliar, reanúdala o cancélala y vuelve a intentarlo. Si no se pueden liberar los lectores a tiempo, no se solicita la expulsión. Un rechazo conserva la unidad en la lista; la preferencia de auxiliar puede quedar desactivada y puedes volver a permitirla con el menú.

## Espejo automático por nombre de carpeta

Selecciona las unidades destino y elige o arrastra tu carpeta origen. Rcopies busca una carpeta con el mismo nombre directamente en la raíz de cada unidad seleccionada. No busca coincidencias recursivas dentro de otras carpetas.

- Si esa carpeta no existe: es un destino de copia normal. Al arrastrar sobre la lista comienza automáticamente; si eliges el origen con el botón Carpeta, queda preparado para pulsar Copiar.
- Si existe y tiene diferencias: se analiza solamente esa carpeta y aparece **«Por favor confirme el proceso de espejo»**, con el aviso **«Los destinos tienen datos que han sido actualizados en el origen»** y las dos acciones finales **Proceder / Cancelar**. La vista de comparación y la selección parcial siguen disponibles.
- Si existe y no hay diferencias por tamaño/fecha: se deja intacta, sin confirmación ni recopia.
- En un grupo mixto, Proceder autoriza los espejos seleccionados y la copia normal en las unidades sin la carpeta. El aviso indica cuántas copias nuevas se harán. Cancelar cancela toda esa operación antes de cambiar datos, incluidas las copias nuevas.

El espejo solo afecta a la carpeta con ese nombre. Los archivos y carpetas fuera de ella permanecen intactos. Un archivo que ocupe el nombre de la carpeta produce un error controlado, sin borrarlo. Los archivos individuales y los orígenes que son raíces de unidad conservan la copia normal. Si eliges el origen antes de seleccionar destinos, pulsa Copiar después de seleccionar las unidades para comparar o copiar.


Usa una carpeta o unidad con **todo el conjunto final** como origen. El espejo añade, actualiza y elimina del destino lo que ya no exista en el origen. Compara tamaño y fecha de modificación sin leer contenidos ni calcular hashes. Copia las diferencias y retira los sobrantes seleccionados.

Después del análisis aparece una comparación visual con origen a la izquierda y destino a la derecha, rutas con extensión, iconos de archivo/carpeta, tamaños y fechas. Verde indica añadir, amarillo actualizar y rosa eliminar. Un selector permite revisar cada destino; la búsqueda y los filtros de diferencias/todos/seleccionados solo cambian lo que se muestra.

Puedes incluir o excluir archivos y carpetas; cada destino conserva su selección. Marcar una carpeta incluye su subárbol. Los cambios de tipo archivo/carpeta se agrupan para evitar reemplazos incompletos. La selección parcial se indica expresamente: conserva los elementos excluidos y no deja necesariamente un espejo completo. La estimación de espacio y los posibles borrados previos se recalculan solo para las operaciones seleccionadas.

Las únicas acciones finales siguen siendo «Proceder» y «Cancelar». No se modifica el destino antes de proceder; Enter y Escape cancelan por defecto. Revisa las eliminaciones y avisos de espacio. Si hace falta borrar sobrantes primero para liberar espacio, el aviso lo indica y proceder autoriza ese borrado irreversible. Si ni así cabe, se bloquea el plan.

No hay transacción ni rollback de todo el destino. Cancelar o fallar puede conservar archivos completados o una carpeta `.DobleCommander-espejo-<id>` con elementos apartados para recuperar; no es una copia de seguridad completa. La ruta aparece en el estado y el log. El espejo no usa auxiliares ni admite incorporaciones durante la operación. En una operación mixta, las incorporaciones quedan suspendidas mientras haya un espejo activo. El modo libre permite espejo a un destino.

## Registros, límites y pruebas

Los registros se guardan en `%LOCALAPPDATA%/Rcopies/Logs`. No se transmiten. El origen debe permanecer estable; no se crean snapshots VSS. Se conservan contenido, jerarquía, carpetas vacías y fechas de archivos según admita el destino; no ACL, propietario, ADS, hardlinks ni sectores de arranque. Se rechazan solapamientos, enlaces simbólicos y junctions. Los datos de nube deben estar descargados.

Los controles de recursos siguen activos internamente. No se puede garantizar velocidad máxima absoluta y ausencia total de impacto a la vez: compartir disco, USB o controlador con otras tareas produce competencia. Un controlador bloqueado puede demorar cancelación y consultas. La lista y opciones no se guardan entre sesiones.

`test.bat` ejecuta las suites. Las pruebas usan archivos propios y simulaciones de expulsión: no se desconectó ni formateó físicamente ningún dispositivo. No se ha certificado un lote físico de 140 GB ni todas las combinaciones USB, SMB, permisos y escalas de Windows.

Todos los destinos autorizados y seleccionados copian en paralelo, sin cola de cuatro ni límite de RAM impuesto por Rcopies. Se conservan los búferes reutilizados y la lectura por bloques. La capacidad y velocidad reales dependen del equipo y los dispositivos. Los cuatro primeros destinos elegibles completados al 100 % se activan individualmente como fuentes auxiliares, sin auditoría SHA-256.

## Identificadores de 63 o 64 caracteres

Los administradores de PC y iPhone admiten identificadores hexadecimales de 63 o 64 caracteres. No rellenan, recortan ni inventan caracteres. Se eliminan separadores de copia (espacios, guiones, saltos de línea y marcas invisibles de formato) y se normalizan las letras a mayúsculas.

Rcopies sigue generando identificadores completos de 64 caracteres, conservando el algoritmo y las licencias anteriores. La versión actualizada admite una licencia firmada de 63 solamente si coincide con el identificador real completo del PC al omitir exactamente uno de sus caracteres. La firma, el producto, el vencimiento y la vinculación al PC se siguen comprobando. Una cadena de 63 que no corresponda a ese PC no lo activa.

Para utilizar licencias de 63 hay que actualizar también Rcopies en el PC del cliente: el EXE anterior las rechaza. No se modifica ni regenera la autoridad de firma. El formato .rcopies-license se conserva.

### Alcance de la comparación rápida

Dos archivos del mismo tamaño y fecha se consideran iguales, aunque su contenido pudiera ser distinto. Sin SHA-256 no se detectan esas diferencias ni una corrupción silenciosa que conserve ambos metadatos. Mantén estables el origen y los auxiliares mientras se copia. Las firmas SHA-256 de las licencias y los SHA-256 usados exclusivamente en los tests no forman parte del trabajo de copia.

Si la carpeta origen no contiene archivos y hay datos que retirar en la carpeta coincidente, la confirmación lo indica expresamente. No se ejecuta ningún borrado antes de Proceder.
