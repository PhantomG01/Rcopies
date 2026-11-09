# Validación de Rcopies

## Versión sin verificación de contenidos y con comparación visual

Se retiraron el cálculo incremental SHA-256, las relecturas del temporal y la auditoría completa de auxiliares. El motor de copia, el planificador de espejo y la promoción automática de auxiliares no leen contenidos para compararlos. SHA-256 solo se conserva para las licencias; la función para verificar archivos en pruebas se encuentra exclusivamente en tests-common/TestHash.cs y no se compila en el EXE.

El primer destino completo se promueve sin esperar a los demás y sin recorrer de nuevo su conjunto de archivos. Se conserva el máximo de cuatro fuentes y la distribución/relevo por bloques. Los fallos de un auxiliar revierten el segmento incompleto y continúan desde una fuente válida. Rehabilitar manualmente un auxiliar solo consulta metadatos.

## Pruebas

- 48 del motor, conflictos, auxiliares, relevo, incorporación tardía y guardas de formateo.
- 31 de espejo completo, recuperación, cancelación, protección y presupuesto compartido de espacio.
- 41 de controlador/interfaz LITE y progreso.
- 20 de licencias/recursos y 3 de medición/ausencia de topes de RAM.
- 34 de auxiliares, selección, licencias libres y expulsión simulada.
- 9 de relevo en vivo.
- 21 de selección/arrastre e inicio automático de copia.
- 10 de paralelismo: 40 destinos avanzan y concluyen; promoción y planificación funcionan con los contenidos bloqueados contra lectura; igual tamaño/fecha se trata intencionalmente como igual; el primero en terminar se promueve mientras otros tres siguen pausados; los demás se incorporan hasta cuatro.
- 22 de comparación y sincronización parcial: filas alineadas y virtuales, dos acciones finales, selección independiente por destino, búsqueda que no modifica la selección, tipos atómicos, copia de hijos y carpetas vacías, conservación de archivos/deletes/tipos excluidos, selección vacía, rechazo de rutas ajenas, convergencia al espejo completo y borrado previo limitado a la selección con espacio recalculado.

La comprobación de integridad mediante hashes se hace desde los tests sobre sus propios archivos para comprobar los resultados, no desde la aplicación durante las copias. No se formatearon ni expulsaron unidades reales. No se garantiza detectar corrupción silenciosa o archivos distintos con idéntico tamaño y fecha. No se ha certificado el rendimiento de 40 discos físicos ni un lote físico de 140 GB.

Se conserva la compatibilidad de licencias 63/64 y la misma autoridad. No se publicaron cambios en el alojamiento del administrador de iPhone ni se modificaron claves o licencias instaladas.

## Espejo automático por coincidencia de nombre

Se elimina el botón Espejo y se conserva el nombre de la carpeta origen por defecto. Elegir una carpeta mediante el selector, arrastrarla o pulsar Copiar usa el mismo analizador automático. El espejo se limita a la subcarpeta homónima de cada unidad, nunca a toda la raíz por esa coincidencia.

24 comprobaciones adicionales: ausencia del botón, carpeta contenedora por defecto, selección de un origen nuevo sin escritura prematura, primera copia por arrastre, alcance homónimo, confirmación previa a toda escritura, cancelación del grupo mixto y limpieza de progreso, actualizaciones/altas/borrados confirmados, preservación de datos fuera de la carpeta, copia en unidades nuevas, mapeo de progreso a las unidades originales, omisión de carpetas iguales, evento DragDrop real con varios destinos, clasificación de unidades, arrastre en el campo de origen, selección parcial, texto exacto de confirmación, dos acciones finales, archivos individuales, colisión carpeta/archivo, licencia libre, cancelación del análisis, aviso de origen sin archivos y conservación de datos al cancelar ese caso.

Las comprobaciones usan unidades simuladas en carpetas temporales. No se formatearon ni expulsaron discos reales. Se conservan las pruebas de metadatos sin SHA-256, paralelismo y auxiliares. La comparación rápida considera iguales archivos con el mismo tamaño y fecha. Cancelar después de haber autorizado y comenzado una operación no revierte los archivos ya completados.
