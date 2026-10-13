IF DB_ID('sacor') IS NULL
    CREATE DATABASE sacor;
GO

USE sacor;
GO

IF OBJECT_ID('dbo.FK_ruta_cotizacion', 'F') IS NOT NULL ALTER TABLE dbo.ruta_trabajo DROP CONSTRAINT FK_ruta_cotizacion;
IF OBJECT_ID('dbo.FK_ruta_contrato', 'F') IS NOT NULL ALTER TABLE dbo.ruta_trabajo DROP CONSTRAINT FK_ruta_contrato;
IF OBJECT_ID('dbo.FK_ruta_camion', 'F') IS NOT NULL ALTER TABLE dbo.ruta_trabajo DROP CONSTRAINT FK_ruta_camion;
IF OBJECT_ID('dbo.FK_contrato_cotizacion', 'F') IS NOT NULL ALTER TABLE dbo.servicio_contrato DROP CONSTRAINT FK_contrato_cotizacion;
IF OBJECT_ID('dbo.gasto_viaje', 'U') IS NOT NULL DROP TABLE dbo.gasto_viaje;
IF OBJECT_ID('dbo.evidencia_foto', 'U') IS NOT NULL DROP TABLE dbo.evidencia_foto;
IF OBJECT_ID('dbo.sesion_movil', 'U') IS NOT NULL DROP TABLE dbo.sesion_movil;
IF OBJECT_ID('dbo.codigo_activacion_movil', 'U') IS NOT NULL DROP TABLE dbo.codigo_activacion_movil;
IF OBJECT_ID('dbo.auditoria', 'U') IS NOT NULL DROP TABLE dbo.auditoria;
IF OBJECT_ID('dbo.viaje_extra_empleado', 'U') IS NOT NULL DROP TABLE dbo.viaje_extra_empleado;
IF OBJECT_ID('dbo.observacion_empleado', 'U') IS NOT NULL DROP TABLE dbo.observacion_empleado;
IF OBJECT_ID('dbo.solicitud_puesto_trabajo', 'U') IS NOT NULL DROP TABLE dbo.solicitud_puesto_trabajo;
IF OBJECT_ID('dbo.cotizacion', 'U') IS NOT NULL DROP TABLE dbo.cotizacion;
IF OBJECT_ID('dbo.usuario_web', 'U') IS NOT NULL DROP TABLE dbo.usuario_web;
IF OBJECT_ID('dbo.viaje_extra', 'U') IS NOT NULL DROP TABLE dbo.viaje_extra;
IF OBJECT_ID('dbo.pesaje', 'U') IS NOT NULL DROP TABLE dbo.pesaje;
IF OBJECT_ID('dbo.limpieza', 'U') IS NOT NULL DROP TABLE dbo.limpieza;
IF OBJECT_ID('dbo.servicio_contrato', 'U') IS NOT NULL DROP TABLE dbo.servicio_contrato;
IF OBJECT_ID('dbo.precio_lugar', 'U') IS NOT NULL DROP TABLE dbo.precio_lugar;
IF OBJECT_ID('dbo.anticipo_sueldo', 'U') IS NOT NULL DROP TABLE dbo.anticipo_sueldo;
IF OBJECT_ID('dbo.pago', 'U') IS NOT NULL DROP TABLE dbo.pago;
IF OBJECT_ID('dbo.servicio_externo', 'U') IS NOT NULL DROP TABLE dbo.servicio_externo;
IF OBJECT_ID('dbo.horario_trabajo', 'U') IS NOT NULL DROP TABLE dbo.horario_trabajo;
IF OBJECT_ID('dbo.camion_conductor', 'U') IS NOT NULL DROP TABLE dbo.camion_conductor;
IF OBJECT_ID('dbo.camion', 'U') IS NOT NULL DROP TABLE dbo.camion;
IF OBJECT_ID('dbo.ruta_empleado', 'U') IS NOT NULL DROP TABLE dbo.ruta_empleado;
IF OBJECT_ID('dbo.ruta_trabajo', 'U') IS NOT NULL DROP TABLE dbo.ruta_trabajo;
IF OBJECT_ID('dbo.cliente', 'U') IS NOT NULL DROP TABLE dbo.cliente;
IF OBJECT_ID('dbo.empleado', 'U') IS NOT NULL DROP TABLE dbo.empleado;
GO

CREATE TABLE empleado (
    id_empleado INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    codigo_empleado NVARCHAR(30) NOT NULL UNIQUE,
    nombre NVARCHAR(150) NOT NULL,
    dui NVARCHAR(12) NOT NULL UNIQUE,
    telefono NVARCHAR(25) NULL,
    sueldo DECIMAL(12,2) NOT NULL,
    fecha_ingreso DATE NOT NULL,
    codigo_af NVARCHAR(30) NULL,
    token NVARCHAR(255) NULL,
    estado NVARCHAR(20) NOT NULL,
    tipo NVARCHAR(30) NOT NULL,
    id_administrador_registro INT NULL,
    CONSTRAINT CK_empleado_tipo CHECK (tipo IN ('AdministradorOperativo', 'AdministradorTecnico', 'Tecnico', 'Motorista', 'Ayudante', 'Mecanico', 'Coordinador')),
    CONSTRAINT CK_empleado_tipo_anterior CHECK (tipo NOT IN ('Ayudante', 'Mecanico', 'Coordinador') OR estado <> 'Activo'),
    CONSTRAINT FK_empleado_registrador FOREIGN KEY (id_administrador_registro)
        REFERENCES empleado(id_empleado)
);
GO

CREATE TABLE cliente (
    id_cliente INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    nombre NVARCHAR(150) NOT NULL,
    tipo NVARCHAR(20) NOT NULL,
    direccion NVARCHAR(250) NULL,
    correo NVARCHAR(254) NULL,
    telefono NVARCHAR(25) NULL,
    nit NVARCHAR(20) NULL,
    dui NVARCHAR(12) NULL,
    giro NVARCHAR(150) NULL,
    nrc NVARCHAR(30) NULL,
    nombre_comercial NVARCHAR(150) NULL,
    id_administrador_registro INT NULL,
    CONSTRAINT FK_cliente_registrador FOREIGN KEY (id_administrador_registro)
        REFERENCES empleado(id_empleado)
);
GO

CREATE TABLE ruta_trabajo (
    id_ruta_trabajo INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    id_administrador INT NOT NULL,
    destino NVARCHAR(200) NOT NULL,
    dinero_gasolina DECIMAL(12,2) NULL,
    tipo NVARCHAR(60) NULL,
    id_servicio_contrato INT NULL,
    id_camion INT NULL,
    CONSTRAINT FK_ruta_admin FOREIGN KEY (id_administrador)
        REFERENCES empleado(id_empleado)
);
GO

CREATE TABLE ruta_empleado (
    id_ruta_empleado INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    id_ruta_trabajo INT NOT NULL,
    id_empleado INT NOT NULL,
    CONSTRAINT FK_ruta_empleado_ruta FOREIGN KEY (id_ruta_trabajo)
        REFERENCES ruta_trabajo(id_ruta_trabajo),
    CONSTRAINT FK_ruta_empleado_empleado FOREIGN KEY (id_empleado)
        REFERENCES empleado(id_empleado)
);
GO

CREATE TABLE camion (
    id_camion INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    placa NVARCHAR(15) NOT NULL UNIQUE,
    nombre NVARCHAR(100) NULL,
    tipo NVARCHAR(60) NULL,
    estado NVARCHAR(40) NULL,
    [dueño] NVARCHAR(150) NULL,
    observacion NVARCHAR(1000) NULL
);
GO

CREATE TABLE camion_conductor (
    id_camion_conductor INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    id_camion INT NOT NULL,
    id_conductor INT NOT NULL,
    fecha DATE NOT NULL,
    CONSTRAINT FK_camion_conductor_camion FOREIGN KEY (id_camion)
        REFERENCES camion(id_camion),
    CONSTRAINT FK_camion_conductor_empleado FOREIGN KEY (id_conductor)
        REFERENCES empleado(id_empleado)
);
GO

CREATE TABLE horario_trabajo (
    id_horario_trabajo INT IDENTITY(1,1) NOT NULL,
    id_empleado_marco INT NOT NULL,
    fecha DATE NOT NULL,
    entrada TIME(0) NOT NULL,
    salida TIME(0) NULL,
    CONSTRAINT PK_horario_trabajo PRIMARY KEY (id_horario_trabajo),
    CONSTRAINT FK_horario_empleado FOREIGN KEY (id_empleado_marco)
        REFERENCES empleado(id_empleado)
);
GO

CREATE TABLE servicio_externo (
    id_servicio_ext INT IDENTITY(1,1) NOT NULL,
    id_administrador INT NOT NULL,
    nombre NVARCHAR(150) NOT NULL,
    cantidad_pago DECIMAL(12,2) NOT NULL,
    direccion NVARCHAR(250) NULL,
    CONSTRAINT PK_servicio_externo PRIMARY KEY (id_servicio_ext),
    CONSTRAINT FK_servicio_externo_admin FOREIGN KEY (id_administrador)
        REFERENCES empleado(id_empleado)
);
GO

CREATE TABLE pago (
    id_pago INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    id_empleado INT NOT NULL,
    fecha DATE NOT NULL,
    cantidad DECIMAL(12,2) NOT NULL,
    descuento DECIMAL(12,2) NOT NULL,
    observacion NVARCHAR(1000) NULL,
    CONSTRAINT FK_pago_empleado FOREIGN KEY (id_empleado)
        REFERENCES empleado(id_empleado)
);
GO

CREATE TABLE anticipo_sueldo (
    id_anticipo INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    id_empleado INT NOT NULL,
    cantidad DECIMAL(12,2) NOT NULL,
    observacion NVARCHAR(1000) NULL,
    fecha DATE NOT NULL,
    CONSTRAINT FK_anticipo_empleado FOREIGN KEY (id_empleado)
        REFERENCES empleado(id_empleado)
);
GO

CREATE TABLE precio_lugar (
    id_precio_lugar INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    tipo NVARCHAR(60) NOT NULL,
    lugar NVARCHAR(200) NOT NULL,
    precio DECIMAL(12,2) NOT NULL,
    fecha_modificacion DATE NULL,
    CONSTRAINT UQ_precio_lugar_tipo_lugar UNIQUE (tipo, lugar)
);
GO

CREATE TABLE servicio_contrato (
    id_servicio_contrato INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    id_ruta_trabajo INT NOT NULL,
    id_cliente INT NOT NULL,
    id_precio_lugar INT NOT NULL,
    observacion NVARCHAR(1000) NULL,
    tipo_transportaje NVARCHAR(80) NULL,
    fecha DATE NULL,
    id_cotizacion INT NULL,
    CONSTRAINT FK_contrato_ruta FOREIGN KEY (id_ruta_trabajo)
        REFERENCES ruta_trabajo(id_ruta_trabajo),
    CONSTRAINT FK_contrato_cliente FOREIGN KEY (id_cliente)
        REFERENCES cliente(id_cliente),
    CONSTRAINT FK_contrato_precio_lugar FOREIGN KEY (id_precio_lugar)
        REFERENCES precio_lugar(id_precio_lugar)
);
GO

CREATE TABLE limpieza (
    id_limpieza INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    id_empleado_subio INT NOT NULL,
    id_servicio_contrato INT NULL,
    id_ruta_trabajo INT NULL,
    fecha DATE NOT NULL,
    observacion NVARCHAR(1000) NULL,
    etapa NVARCHAR(60) NULL,
    CONSTRAINT FK_limpieza_ruta FOREIGN KEY (id_ruta_trabajo)
        REFERENCES ruta_trabajo(id_ruta_trabajo),
    CONSTRAINT FK_limpieza_empleado FOREIGN KEY (id_empleado_subio)
        REFERENCES empleado(id_empleado),
    CONSTRAINT FK_limpieza_contrato FOREIGN KEY (id_servicio_contrato)
        REFERENCES servicio_contrato(id_servicio_contrato)
);
GO

CREATE TABLE pesaje (
    id_pesaje INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    id_servicio_contrato INT NOT NULL,
    id_empleado_subio INT NOT NULL,
    id_tecnico_transporte INT NOT NULL,
    id_precio_lugar INT NOT NULL,
    fecha DATE NULL,
    peso_total DECIMAL(12,2) NULL,
    etapa NVARCHAR(60) NULL,
    id_ruta_trabajo INT NULL,
    precio_tonelada DECIMAL(12,2) NULL,
    CONSTRAINT FK_pesaje_ruta FOREIGN KEY (id_ruta_trabajo)
        REFERENCES ruta_trabajo(id_ruta_trabajo),
    CONSTRAINT FK_pesaje_contrato FOREIGN KEY (id_servicio_contrato)
        REFERENCES servicio_contrato(id_servicio_contrato),
    CONSTRAINT FK_pesaje_empleado_subio FOREIGN KEY (id_empleado_subio)
        REFERENCES empleado(id_empleado),
    CONSTRAINT FK_pesaje_transportista FOREIGN KEY (id_tecnico_transporte)
        REFERENCES empleado(id_empleado),
    CONSTRAINT FK_pesaje_precio_lugar FOREIGN KEY (id_precio_lugar)
        REFERENCES precio_lugar(id_precio_lugar)
);
GO

CREATE TABLE viaje_extra (
    id_viaje_extra INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    id_cliente INT NOT NULL,
    id_administrador INT NOT NULL,
    id_camion INT NOT NULL,
    tipo NVARCHAR(60) NULL,
    precio DECIMAL(12,2) NULL,
    pago DECIMAL(12,2) NULL,
    cantidad INT NULL,
    id_ruta_trabajo INT NULL,
    CONSTRAINT FK_viaje_extra_ruta FOREIGN KEY (id_ruta_trabajo)
        REFERENCES ruta_trabajo(id_ruta_trabajo),
    CONSTRAINT FK_viaje_cliente FOREIGN KEY (id_cliente)
        REFERENCES cliente(id_cliente),
    CONSTRAINT FK_viaje_admin FOREIGN KEY (id_administrador)
        REFERENCES empleado(id_empleado),
    CONSTRAINT FK_viaje_camion FOREIGN KEY (id_camion)
        REFERENCES camion(id_camion)
);
GO

CREATE TABLE usuario_web (
    id_usuario_web INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    nombre NVARCHAR(150) NOT NULL,
    dui NVARCHAR(12) NOT NULL UNIQUE,
    telefono NVARCHAR(25) NULL,
    email NVARCHAR(254) NULL
);
GO

CREATE TABLE cotizacion (
    id_cotizacion INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    id_usuario_web INT NOT NULL,
    id_administrador_analista INT NULL,
    fecha DATE NOT NULL,
    estado NVARCHAR(50) NOT NULL,
    tipo_servicio NVARCHAR(80) NULL,
    descripcion NVARCHAR(1000) NULL,
    CONSTRAINT FK_cotizacion_usuario FOREIGN KEY (id_usuario_web)
        REFERENCES usuario_web(id_usuario_web),
    CONSTRAINT FK_cotizacion_analista FOREIGN KEY (id_administrador_analista)
        REFERENCES empleado(id_empleado)
);
GO

CREATE TABLE solicitud_puesto_trabajo (
    id_solicitud INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    id_usuario_web INT NOT NULL,
    id_administrador_analista INT NULL,
    tipo_puesto NVARCHAR(100) NOT NULL,
    fecha DATE NOT NULL,
    CONSTRAINT FK_solicitud_usuario FOREIGN KEY (id_usuario_web)
        REFERENCES usuario_web(id_usuario_web),
    CONSTRAINT FK_solicitud_analista FOREIGN KEY (id_administrador_analista)
        REFERENCES empleado(id_empleado)
);
GO

CREATE TABLE observacion_empleado (
    id_observacion INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    id_administrador_autor INT NOT NULL,
    id_empleado INT NOT NULL,
    fecha DATE NOT NULL,
    tipo NVARCHAR(60) NULL,
    titulo NVARCHAR(200) NOT NULL,
    CONSTRAINT FK_observacion_autor FOREIGN KEY (id_administrador_autor)
        REFERENCES empleado(id_empleado),
    CONSTRAINT FK_observacion_empleado FOREIGN KEY (id_empleado)
        REFERENCES empleado(id_empleado)
);
GO

CREATE TABLE viaje_extra_empleado (
    id_viaje_extra INT NOT NULL,
    id_empleado INT NOT NULL,
    CONSTRAINT PK_viaje_extra_empleado PRIMARY KEY (id_viaje_extra, id_empleado),
    CONSTRAINT FK_viaje_emp_viaje FOREIGN KEY (id_viaje_extra)
        REFERENCES viaje_extra(id_viaje_extra),
    CONSTRAINT FK_viaje_emp_empleado FOREIGN KEY (id_empleado)
        REFERENCES empleado(id_empleado)
);
GO

CREATE TABLE auditoria (
    id_auditoria INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    id_empleado INT NULL,
    tabla_afectada NVARCHAR(128) NOT NULL,
    id_registro NVARCHAR(100) NOT NULL,
    accion NVARCHAR(10) NOT NULL,
    fecha_hora DATETIME2(0) NOT NULL DEFAULT SYSDATETIME(),
    valor_anterior NVARCHAR(MAX) NULL,
    valor_nuevo NVARCHAR(MAX) NULL,
    detalle NVARCHAR(1000) NULL,
    CONSTRAINT FK_auditoria_empleado FOREIGN KEY (id_empleado)
        REFERENCES empleado(id_empleado),
    CONSTRAINT CK_auditoria_accion CHECK (accion IN ('INSERT', 'UPDATE', 'DELETE'))
);
GO

CREATE INDEX IX_auditoria_tabla_fecha ON auditoria (tabla_afectada, fecha_hora DESC);
GO

CREATE TABLE codigo_activacion_movil (
    id_codigo_activacion INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    id_empleado INT NOT NULL,
    codigo_hash CHAR(64) NOT NULL,
    id_administrador_generador INT NULL,
    creado_utc DATETIME2(0) NOT NULL,
    expira_utc DATETIME2(0) NOT NULL,
    usado_utc DATETIME2(0) NULL,
    revocado_utc DATETIME2(0) NULL,
    CONSTRAINT UQ_codigo_activacion_movil_hash UNIQUE (codigo_hash),
    CONSTRAINT FK_codigo_activacion_movil_empleado FOREIGN KEY (id_empleado)
        REFERENCES empleado(id_empleado),
    CONSTRAINT FK_codigo_activacion_movil_administrador FOREIGN KEY (id_administrador_generador)
        REFERENCES empleado(id_empleado)
);
GO

CREATE TABLE sesion_movil (
    id_sesion_movil INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    id_empleado INT NOT NULL,
    token_hash CHAR(64) NOT NULL,
    creado_utc DATETIME2(0) NOT NULL,
    expira_utc DATETIME2(0) NOT NULL,
    revocado_utc DATETIME2(0) NULL,
    CONSTRAINT UQ_sesion_movil_token UNIQUE (token_hash),
    CONSTRAINT FK_sesion_movil_empleado FOREIGN KEY (id_empleado)
        REFERENCES empleado(id_empleado)
);
GO

CREATE INDEX IX_sesion_movil_empleado ON sesion_movil (id_empleado, revocado_utc);
GO

CREATE TABLE evidencia_foto (
    id_evidencia_foto INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    id_limpieza INT NOT NULL,
    tipo_contenido NVARCHAR(30) NOT NULL,
    contenido VARBINARY(MAX) NOT NULL,
    tamano_bytes INT NOT NULL,
    creado_utc DATETIME2(0) NOT NULL,
    CONSTRAINT FK_evidencia_foto_limpieza FOREIGN KEY (id_limpieza)
        REFERENCES limpieza(id_limpieza),
    CONSTRAINT CK_evidencia_foto_tipo CHECK (tipo_contenido IN ('image/jpeg', 'image/png')),
    CONSTRAINT CK_evidencia_foto_tamano CHECK (tamano_bytes BETWEEN 1 AND 3145728)
);
GO

CREATE INDEX IX_evidencia_foto_limpieza ON evidencia_foto (id_limpieza);
GO

ALTER TABLE ruta_trabajo ADD CONSTRAINT FK_ruta_contrato FOREIGN KEY (id_servicio_contrato)
    REFERENCES servicio_contrato(id_servicio_contrato);
GO

ALTER TABLE ruta_trabajo ADD CONSTRAINT FK_ruta_camion FOREIGN KEY (id_camion)
    REFERENCES camion(id_camion);
GO

ALTER TABLE servicio_contrato ADD CONSTRAINT FK_contrato_cotizacion FOREIGN KEY (id_cotizacion)
    REFERENCES cotizacion(id_cotizacion);
GO

CREATE INDEX IX_ruta_trabajo_contrato ON ruta_trabajo (id_servicio_contrato);
GO

CREATE UNIQUE INDEX UX_contrato_cotizacion ON servicio_contrato (id_cotizacion) WHERE id_cotizacion IS NOT NULL;
GO

CREATE INDEX IX_limpieza_ruta ON limpieza (id_ruta_trabajo);
GO

CREATE INDEX IX_viaje_extra_ruta ON viaje_extra (id_ruta_trabajo);
GO

CREATE TABLE gasto_viaje (
    id_gasto_viaje INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    id_ruta_trabajo INT NOT NULL,
    id_empleado INT NOT NULL,
    fecha DATE NOT NULL,
    concepto NVARCHAR(40) NOT NULL,
    monto DECIMAL(12,2) NOT NULL,
    descripcion NVARCHAR(250) NULL,
    CONSTRAINT FK_gasto_viaje_ruta FOREIGN KEY (id_ruta_trabajo)
        REFERENCES ruta_trabajo(id_ruta_trabajo),
    CONSTRAINT FK_gasto_viaje_empleado FOREIGN KEY (id_empleado)
        REFERENCES empleado(id_empleado),
    CONSTRAINT CK_gasto_viaje_concepto CHECK (concepto IN (N'Combustible', N'Peaje', N'Alimentacion', N'Mantenimiento', N'Otro')),
    CONSTRAINT CK_gasto_viaje_monto CHECK (monto > 0 AND monto <= 100000)
);
GO

CREATE INDEX IX_gasto_viaje_ruta ON gasto_viaje (id_ruta_trabajo);
GO

CREATE INDEX IX_pesaje_ruta ON pesaje (id_ruta_trabajo);
GO
