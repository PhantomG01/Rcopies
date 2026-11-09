# Validación del administrador de licencias

## Compatibilidad de identificadores 63/64

Se prueba el identificador concreto de 63 recibido del usuario sin alterarlo y su firma desde JavaScript. El verificador .NET actualizado lo acepta únicamente contra un identificador completo compatible. Se prueban omisiones iniciales, intermedias y finales, rechazo de otro PC, longitudes 62/65 y caracteres no hexadecimales. Se mantienen la generación de 64 caracteres y la activación por igualdad exacta de las licencias de 64. El cliente anterior exige actualizarse para admitir 63.
