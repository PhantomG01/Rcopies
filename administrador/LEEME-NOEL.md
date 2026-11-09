# Rcopies · Administrador privado

Este paquete es solo para Noel. No se entrega a clientes: contiene el archivo privado de emisión y tu licencia. Al cliente entrégale únicamente el paquete Rcopies y su archivo de licencia.

## Uso

Descomprime conservando la estructura. Abre `portable/RcopiesAdmin.exe`. **No hay campo de clave privada ni hay que seleccionarla:** se carga automáticamente `Claves-Noel/Noel-Rcopies-private.pem`, junto a la carpeta portable. No la regeneres: es la misma autoridad de la edición anterior. Si falta el archivo, restaura la carpeta de este paquete.

Escribe titular e identificador del PC, elige vencimiento o «Sin vencimiento» y pulsa «Generar licencia». El cliente obtiene su identificador desde «Licencia» en Rcopies e importa el `.rcopies-license` que le entregues. Para renovar genera otro archivo para el mismo PC y otra fecha. «Historial de licencias» consulta el registro local de emisiones.

Rcopies abre sin licencia y admite una copia o espejo a un destino por operación. La licencia permite varios destinos e incorporaciones durante un lote. Se siguen aceptando las licencias válidas de Clonova. El identificador de instalación y la autoridad se conservan pese al cambio de nombre.

Para activar tu PC importa `Claves-Noel/Noel-PC.rcopies-license` desde «Licencia» en Rcopies. No es universal: está firmada para este PC. Reinstalar o clonar Windows puede cambiar o duplicar su identificador.

## Proteger el administrador

El archivo inicial se entrega sin contraseña para que puedas empezar sin haber compartido una con el asistente. «Proteger administrador…» pide una contraseña nueva de al menos 12 caracteres y su confirmación; cifra el archivo de emisión en su ubicación automática. A partir de entonces se solicita la contraseña al emitir licencias. No aparecen rutas ni campos de clave en la ventana principal.

Guarda copias privadas del paquete y conserva la contraseña. No distribuyas este ZIP ni el archivo de emisión. Perder la clave impide emitir para los clientes actuales; cambiarla exige distribuir un nuevo cliente con otra clave pública. No hay recuperación por Internet.

## Exportar para el iPhone

«Exportar para iPhone…» crea un archivo `.rcopies-vault` cifrado con una contraseña nueva de al menos 12 caracteres. Se conserva la misma autoridad; no se regenera la clave ni se alteran las licencias. Importa ese archivo desde Archivos en la aplicación web instalada en la pantalla de inicio. La guía se entrega con Rcopies-iPhone. No subas el archivo cifrado, este administrador ni Claves-Noel al alojamiento web.

La exportación emplea PBKDF2-SHA256 (600.000 iteraciones) y AES-256-GCM con sal e IV aleatorios. Necesitas la contraseña al desbloquearla en el teléfono. Si ya protegiste el administrador de Windows, se pide primero su contraseña existente. Conservar el archivo cifrado y su contraseña permite restaurarlo si iOS elimina el almacenamiento web.

## Recursos del cliente actual

La versión actual de Rcopies copia hacia todos los destinos autorizados y seleccionados en paralelo, sin cola de cuatro ni techo de RAM impuesto por la aplicación. Conserva lectura por bloques y búferes reutilizados. El medidor muestra RAM usada y libre. Las cuatro plazas auxiliares son fuentes de lectura completadas y auditadas, no un límite de destinos activos.

## Límites offline

No hay revocación remota instantánea, servidor ni recuento central de instalaciones. Usa fechas de vencimiento para controlar renovaciones; una licencia perpetua no puede retirarse remotamente. La firma impide editar o emitir licencias válidas sin la clave, pero no hace inviolable un ejecutable offline ni impide manipulación avanzada del reloj o de la instalación de Windows. Las versiones anteriores sin licencia no se desactivan automáticamente.

`build.bat` recompila el administrador con .NET SDK 10 y no cambia los archivos de emisión. El cliente se recompila desde su propio proyecto. Los paquetes se entregan separados y con sus SHA-256.

## Identificadores de 63 o 64 caracteres

Los administradores de PC y iPhone admiten identificadores hexadecimales de 63 o 64 caracteres. No rellenan, recortan ni inventan caracteres. Se eliminan separadores de copia (espacios, guiones, saltos de línea y marcas invisibles de formato) y se normalizan las letras a mayúsculas.

Rcopies sigue generando identificadores completos de 64 caracteres, conservando el algoritmo y las licencias anteriores. La versión actualizada admite una licencia firmada de 63 solamente si coincide con el identificador real completo del PC al omitir exactamente uno de sus caracteres. La firma, el producto, el vencimiento y la vinculación al PC se siguen comprobando. Una cadena de 63 que no corresponda a ese PC no lo activa.

Para utilizar licencias de 63 hay que actualizar también Rcopies en el PC del cliente: el EXE anterior las rechaza. No se modifica ni regenera la autoridad de firma. El formato .rcopies-license se conserva.
