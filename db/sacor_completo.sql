IF DB_ID('sacor') IS NULL
    CREATE DATABASE sacor;
GO

USE sacor;
GO

IF OBJECT_ID('dbo.FK_ruta_cotizacion', 'F') IS NOT NULL ALTER TABLE dbo.ruta_trabajo DROP CONSTRAINT FK_ruta_cotizacion;
IF OBJECT_ID('dbo.FK_ruta_contrato', 'F') IS NOT NULL ALTER TABLE dbo.ruta_trabajo DROP CONSTRAINT FK_ruta_contrato;
IF OBJECT_ID('dbo.FK_ruta_camion', 'F') IS NOT NULL ALTER TABLE dbo.ruta_trabajo DROP CONSTRAINT FK_ruta_camion;
IF OBJECT_ID('dbo.FK_contrato_cotizacion', 'F') IS NOT NULL ALTER TABLE dbo.servicio_contrato DROP CONSTRAINT FK_contrato_cotizacion;
IF OBJECT_ID('dbo.conversacion_whatsapp', 'U') IS NOT NULL DROP TABLE dbo.conversacion_whatsapp;
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

CREATE TABLE conversacion_whatsapp (
    id_conversacion INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    telefono NVARCHAR(20) NOT NULL,
    paso NVARCHAR(30) NOT NULL,
    datos NVARCHAR(2000) NULL,
    ultimo_mensaje_id NVARCHAR(128) NULL,
    actualizado_utc DATETIME2 NOT NULL,
    id_cotizacion INT NULL,
    CONSTRAINT UQ_conversacion_whatsapp_telefono UNIQUE (telefono),
    CONSTRAINT FK_conversacion_whatsapp_cotizacion FOREIGN KEY (id_cotizacion)
        REFERENCES cotizacion(id_cotizacion)
);
GO

CREATE INDEX IX_pesaje_ruta ON pesaje (id_ruta_trabajo);
GO

IF TYPE_ID('dbo.ListaEmpleados') IS NULL
    CREATE TYPE dbo.ListaEmpleados AS TABLE (id_empleado INT NOT NULL PRIMARY KEY);
GO

CREATE OR ALTER PROCEDURE dbo.sp_CrearViajeExtraConTripulacion
    @id_cliente INT = NULL,
    @id_administrador INT,
    @id_camion INT,
    @tipo NVARCHAR(50) = NULL,
    @precio DECIMAL(12,2) = NULL,
    @pago DECIMAL(12,2) = NULL,
    @cantidad INT = 1,
    @fecha DATE = NULL,
    @id_servicio_contrato INT = NULL,
    @empleados dbo.ListaEmpleados READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @fecha IS NULL
        SET @fecha = CAST(GETDATE() AS DATE);

    IF @id_servicio_contrato IS NOT NULL
    BEGIN
        SELECT @id_cliente = id_cliente
        FROM servicio_contrato
        WHERE id_servicio_contrato = @id_servicio_contrato;

        IF @@ROWCOUNT = 0
            THROW 50006, N'El contrato no existe.', 1;
    END;

    IF NOT EXISTS (SELECT 1 FROM cliente WHERE id_cliente = @id_cliente)
        THROW 50001, N'El cliente no existe.', 1;

    IF NOT EXISTS (
        SELECT 1 FROM empleado
        WHERE id_empleado = @id_administrador
          AND tipo IN ('AdministradorOperativo', 'AdministradorTecnico')
          AND estado = 'Activo'
    )
        THROW 50002, N'El administrador no existe o no está activo.', 1;

    IF NOT EXISTS (SELECT 1 FROM camion WHERE id_camion = @id_camion)
        THROW 50003, N'El camión no existe.', 1;

    IF NOT EXISTS (SELECT 1 FROM @empleados)
        THROW 50004, N'Debe asignarse al menos un empleado al viaje.', 1;

    IF EXISTS (
        SELECT 1
        FROM @empleados l
        LEFT JOIN empleado e ON e.id_empleado = l.id_empleado
        WHERE e.id_empleado IS NULL
           OR e.estado <> 'Activo'
           OR e.tipo NOT IN ('Motorista', 'Tecnico')
    )
        THROW 50005, N'Solo se pueden asignar motoristas y técnicos activos.', 1;

    DECLARE @texto NVARCHAR(50) = COALESCE(NULLIF(LTRIM(RTRIM(@tipo)), ''), N'Viaje extra');
    DECLARE @id_ruta_trabajo INT = NULL;

    BEGIN TRY
        BEGIN TRANSACTION;

        IF @id_servicio_contrato IS NOT NULL
        BEGIN
            DECLARE @destino NVARCHAR(200) = (
                SELECT r.destino
                FROM servicio_contrato s
                JOIN ruta_trabajo r ON r.id_ruta_trabajo = s.id_ruta_trabajo
                WHERE s.id_servicio_contrato = @id_servicio_contrato
            );

            IF @destino LIKE N'\[____-__-__\] %' ESCAPE N'\'
                SET @destino = SUBSTRING(@destino, 14, 200);

            INSERT INTO ruta_trabajo (id_administrador, destino, tipo, id_servicio_contrato, id_camion)
            VALUES (
                @id_administrador,
                LEFT(N'[' + CONVERT(CHAR(10), @fecha, 23) + N'] ' + COALESCE(@destino, N'Sin destino'), 200),
                LEFT(N'Viaje extra: ' + @texto, 60),
                @id_servicio_contrato,
                @id_camion
            );

            SET @id_ruta_trabajo = SCOPE_IDENTITY();

            INSERT INTO ruta_empleado (id_ruta_trabajo, id_empleado)
            SELECT @id_ruta_trabajo, id_empleado FROM @empleados;
        END;

        INSERT INTO viaje_extra (
            id_cliente, id_administrador, id_camion,
            tipo, precio, pago, cantidad, id_ruta_trabajo
        )
        VALUES (
            @id_cliente, @id_administrador, @id_camion,
            LEFT(N'[' + CONVERT(CHAR(7), @fecha, 23) + N'] ' + @texto, 60),
            @precio, @pago, COALESCE(@cantidad, 1), @id_ruta_trabajo
        );

        DECLARE @id_viaje_extra INT = SCOPE_IDENTITY();

        INSERT INTO viaje_extra_empleado (id_viaje_extra, id_empleado)
        SELECT @id_viaje_extra, id_empleado FROM @empleados;

        COMMIT TRANSACTION;

        SELECT @id_viaje_extra AS id_viaje_extra, @id_ruta_trabajo AS id_ruta_trabajo;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_CrearRutaConEmpleados
    @id_administrador INT,
    @destino NVARCHAR(185),
    @fecha DATE = NULL,
    @dinero_gasolina DECIMAL(12,2) = NULL,
    @tipo NVARCHAR(60) = NULL,
    @id_servicio_contrato INT = NULL,
    @id_camion INT = NULL,
    @empleados dbo.ListaEmpleados READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (
        SELECT 1 FROM empleado
        WHERE id_empleado = @id_administrador
          AND tipo IN ('AdministradorOperativo', 'AdministradorTecnico')
          AND estado = 'Activo'
    )
        THROW 50010, N'El administrador no existe o no está activo.', 1;

    IF NULLIF(LTRIM(RTRIM(@destino)), '') IS NULL
        THROW 50011, N'El destino es obligatorio.', 1;

    IF NOT EXISTS (SELECT 1 FROM @empleados)
        THROW 50012, N'Debe asignarse al menos un empleado a la ruta.', 1;

    IF EXISTS (
        SELECT 1
        FROM @empleados l
        LEFT JOIN empleado e ON e.id_empleado = l.id_empleado
        WHERE e.id_empleado IS NULL
           OR e.estado <> 'Activo'
           OR e.tipo NOT IN ('Motorista', 'Tecnico')
    )
        THROW 50013, N'Solo se pueden asignar motoristas y técnicos activos.', 1;

    IF @id_servicio_contrato IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM servicio_contrato WHERE id_servicio_contrato = @id_servicio_contrato)
        THROW 50014, N'El contrato no existe.', 1;

    IF @id_camion IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM camion WHERE id_camion = @id_camion)
        THROW 50015, N'El camión no existe.', 1;

    IF @fecha IS NULL
        SET @fecha = CAST(GETDATE() AS DATE);

    BEGIN TRY
        BEGIN TRANSACTION;

        INSERT INTO ruta_trabajo (
            id_administrador, destino, dinero_gasolina, tipo, id_servicio_contrato, id_camion
        )
        VALUES (
            @id_administrador,
            N'[' + CONVERT(CHAR(10), @fecha, 23) + N'] ' + LTRIM(RTRIM(@destino)),
            @dinero_gasolina,
            @tipo,
            @id_servicio_contrato,
            @id_camion
        );

        DECLARE @id_ruta_trabajo INT = SCOPE_IDENTITY();

        INSERT INTO ruta_empleado (id_ruta_trabajo, id_empleado)
        SELECT @id_ruta_trabajo, id_empleado FROM @empleados;

        COMMIT TRANSACTION;

        SELECT @id_ruta_trabajo AS id_ruta_trabajo;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_RegistrarRecoleccion
    @id_empleado INT,
    @id_ruta_trabajo INT,
    @observacion NVARCHAR(1000),
    @etapa NVARCHAR(60) = N'Recolección finalizada'
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (
        SELECT 1 FROM empleado
        WHERE id_empleado = @id_empleado
          AND estado = 'Activo'
          AND tipo = 'Tecnico'
    )
        THROW 50020, N'El técnico no existe o no está activo.', 1;

    IF NOT EXISTS (
        SELECT 1 FROM ruta_empleado
        WHERE id_ruta_trabajo = @id_ruta_trabajo
          AND id_empleado = @id_empleado
    )
        THROW 50021, N'El técnico no está asignado a ese viaje.', 1;

    IF NULLIF(LTRIM(RTRIM(@observacion)), '') IS NULL
        THROW 50022, N'La observación es obligatoria.', 1;

    DECLARE @id_servicio_contrato INT = (
        SELECT id_servicio_contrato FROM ruta_trabajo WHERE id_ruta_trabajo = @id_ruta_trabajo
    );

    IF @id_servicio_contrato IS NULL
        SELECT TOP 1 @id_servicio_contrato = id_servicio_contrato
        FROM servicio_contrato
        WHERE id_ruta_trabajo = @id_ruta_trabajo
        ORDER BY id_servicio_contrato;

    INSERT INTO limpieza (
        id_empleado_subio, id_servicio_contrato, id_ruta_trabajo,
        fecha, observacion, etapa
    )
    VALUES (
        @id_empleado, @id_servicio_contrato, @id_ruta_trabajo,
        CAST(GETDATE() AS DATE), LTRIM(RTRIM(@observacion)), @etapa
    );

    SELECT CAST(SCOPE_IDENTITY() AS INT) AS id_limpieza;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_RegistrarPesaje
    @id_servicio_contrato INT,
    @id_empleado_subio INT,
    @id_tecnico_transporte INT,
    @id_precio_lugar INT,
    @peso_total DECIMAL(12,2),
    @etapa NVARCHAR(60) = NULL,
    @fecha DATE = NULL,
    @id_ruta_trabajo INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM servicio_contrato WHERE id_servicio_contrato = @id_servicio_contrato)
        THROW 50030, N'El servicio de contrato no existe.', 1;

    IF NOT EXISTS (SELECT 1 FROM empleado WHERE id_empleado = @id_empleado_subio AND estado = 'Activo')
        THROW 50031, N'El empleado que registra el pesaje no existe o no está activo.', 1;

    IF NOT EXISTS (
        SELECT 1 FROM empleado
        WHERE id_empleado = @id_tecnico_transporte
          AND estado = 'Activo'
          AND tipo = 'Tecnico'
    )
        THROW 50032, N'El técnico responsable no existe o no está activo.', 1;

    DECLARE @precio DECIMAL(12,2) = (
        SELECT precio FROM precio_lugar WHERE id_precio_lugar = @id_precio_lugar
    );

    IF @precio IS NULL
        THROW 50033, N'El precio/lugar seleccionado no existe.', 1;

    IF @peso_total IS NULL OR @peso_total <= 0 OR @peso_total > 100000
        THROW 50034, N'El peso total debe estar entre 0.01 y 100000 libras.', 1;

    IF @id_ruta_trabajo IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM ruta_trabajo WHERE id_ruta_trabajo = @id_ruta_trabajo)
        THROW 50035, N'El viaje no existe.', 1;

    INSERT INTO pesaje (
        id_servicio_contrato, id_empleado_subio, id_tecnico_transporte,
        id_precio_lugar, fecha, peso_total, etapa, id_ruta_trabajo, precio_tonelada
    )
    VALUES (
        @id_servicio_contrato, @id_empleado_subio, @id_tecnico_transporte,
        @id_precio_lugar, COALESCE(@fecha, CAST(GETDATE() AS DATE)), @peso_total,
        @etapa, @id_ruta_trabajo, @precio
    );

    SELECT CAST(SCOPE_IDENTITY() AS INT) AS id_pesaje;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_RegistrarGastoViaje
    @id_empleado INT,
    @id_ruta_trabajo INT,
    @concepto NVARCHAR(40),
    @monto DECIMAL(12,2),
    @descripcion NVARCHAR(250) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (
        SELECT 1 FROM ruta_empleado
        WHERE id_ruta_trabajo = @id_ruta_trabajo
          AND id_empleado = @id_empleado
    )
        THROW 50120, N'El empleado no está asignado a ese viaje.', 1;

    IF @concepto NOT IN (N'Combustible', N'Peaje', N'Alimentacion', N'Mantenimiento', N'Otro')
        THROW 50121, N'El concepto debe ser Combustible, Peaje, Alimentacion, Mantenimiento u Otro.', 1;

    IF @monto IS NULL OR @monto <= 0 OR @monto > 100000
        THROW 50122, N'El monto debe estar entre 0.01 y 100000.', 1;

    INSERT INTO gasto_viaje (id_ruta_trabajo, id_empleado, fecha, concepto, monto, descripcion)
    VALUES (
        @id_ruta_trabajo, @id_empleado, CAST(GETDATE() AS DATE),
        @concepto, @monto, NULLIF(LTRIM(RTRIM(@descripcion)), '')
    );

    SELECT CAST(SCOPE_IDENTITY() AS INT) AS id_gasto_viaje;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_MarcarEntradaEmpleado
    @id_empleado INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (
        SELECT 1 FROM empleado
        WHERE id_empleado = @id_empleado
          AND estado = 'Activo'
          AND tipo IN ('Motorista', 'Tecnico')
    )
        THROW 50040, N'El empleado no existe, no está activo o no marca horario.', 1;

    BEGIN TRANSACTION;

    IF EXISTS (
        SELECT 1
        FROM horario_trabajo WITH (UPDLOCK, HOLDLOCK)
        WHERE id_empleado_marco = @id_empleado
          AND salida IS NULL
    )
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 50041, N'El empleado ya tiene una jornada abierta.', 1;
    END;

    INSERT INTO horario_trabajo (id_empleado_marco, fecha, entrada, salida)
    VALUES (@id_empleado, CAST(GETDATE() AS DATE), CAST(GETDATE() AS TIME(0)), NULL);

    DECLARE @id INT = SCOPE_IDENTITY();

    COMMIT TRANSACTION;

    SELECT @id AS id_horario_trabajo;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_MarcarSalidaEmpleado
    @id_empleado INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @id_horario_trabajo INT;

    SELECT TOP 1 @id_horario_trabajo = id_horario_trabajo
    FROM horario_trabajo
    WHERE id_empleado_marco = @id_empleado
      AND salida IS NULL
    ORDER BY id_horario_trabajo DESC;

    IF @id_horario_trabajo IS NULL
        THROW 50050, N'No existe una jornada abierta para este empleado.', 1;

    UPDATE horario_trabajo
    SET salida = CAST(GETDATE() AS TIME(0))
    WHERE id_horario_trabajo = @id_horario_trabajo;

    SELECT @id_horario_trabajo AS id_horario_trabajo;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_RegistrarPagoEmpleado
    @id_empleado INT,
    @cantidad DECIMAL(12,2),
    @descuento DECIMAL(12,2) = 0,
    @observacion NVARCHAR(1000) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM empleado WHERE id_empleado = @id_empleado)
        THROW 50060, N'El empleado no existe.', 1;

    IF @cantidad < 0
        THROW 50061, N'La cantidad del pago no puede ser negativa.', 1;

    IF @descuento < 0 OR @descuento > @cantidad
        THROW 50062, N'El descuento no puede ser negativo ni mayor que el pago.', 1;

    INSERT INTO pago (id_empleado, fecha, cantidad, descuento, observacion)
    VALUES (@id_empleado, CAST(GETDATE() AS DATE), @cantidad, @descuento, @observacion);

    SELECT CAST(SCOPE_IDENTITY() AS INT) AS id_pago;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_RegistrarAnticipoSueldo
    @id_empleado INT,
    @cantidad DECIMAL(12,2),
    @observacion NVARCHAR(1000) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM empleado WHERE id_empleado = @id_empleado)
        THROW 50070, N'El empleado no existe.', 1;

    IF @cantidad <= 0
        THROW 50071, N'El anticipo debe ser mayor que cero.', 1;

    INSERT INTO anticipo_sueldo (id_empleado, cantidad, observacion, fecha)
    VALUES (@id_empleado, @cantidad, @observacion, CAST(GETDATE() AS DATE));

    SELECT CAST(SCOPE_IDENTITY() AS INT) AS id_anticipo;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_RegistrarObservacionEmpleado
    @id_administrador_autor INT,
    @id_empleado INT,
    @tipo NVARCHAR(60) = NULL,
    @titulo NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (
        SELECT 1 FROM empleado
        WHERE id_empleado = @id_administrador_autor
          AND tipo IN ('AdministradorOperativo', 'AdministradorTecnico')
          AND estado = 'Activo'
    )
        THROW 50080, N'El administrador autor no existe o no está activo.', 1;

    IF NOT EXISTS (SELECT 1 FROM empleado WHERE id_empleado = @id_empleado)
        THROW 50081, N'El empleado no existe.', 1;

    IF NULLIF(LTRIM(RTRIM(@titulo)), '') IS NULL
        THROW 50082, N'El título es obligatorio.', 1;

    INSERT INTO observacion_empleado (id_administrador_autor, id_empleado, fecha, tipo, titulo)
    VALUES (@id_administrador_autor, @id_empleado, CAST(GETDATE() AS DATE), @tipo, @titulo);

    SELECT CAST(SCOPE_IDENTITY() AS INT) AS id_observacion;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_AsignarConductorCamion
    @id_camion INT,
    @id_conductor INT,
    @fecha DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM camion WHERE id_camion = @id_camion)
        THROW 50090, N'El camión no existe.', 1;

    IF NOT EXISTS (
        SELECT 1 FROM empleado
        WHERE id_empleado = @id_conductor
          AND estado = 'Activo'
          AND tipo = 'Motorista'
    )
        THROW 50091, N'El conductor no existe, no está activo o no es motorista.', 1;

    IF @fecha IS NULL
        SET @fecha = CAST(GETDATE() AS DATE);

    IF EXISTS (
        SELECT 1 FROM camion_conductor
        WHERE id_camion = @id_camion
          AND id_conductor = @id_conductor
          AND fecha = @fecha
    )
        THROW 50092, N'Ese conductor ya está asignado a ese camión en esa fecha.', 1;

    INSERT INTO camion_conductor (id_camion, id_conductor, fecha)
    VALUES (@id_camion, @id_conductor, @fecha);

    SELECT CAST(SCOPE_IDENTITY() AS INT) AS id_camion_conductor;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_RegistrarServicioContrato
    @id_ruta_trabajo INT,
    @id_cliente INT,
    @id_precio_lugar INT,
    @observacion NVARCHAR(1000) = NULL,
    @tipo_transportaje NVARCHAR(80) = NULL,
    @fecha DATE = NULL,
    @id_cotizacion INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM ruta_trabajo WHERE id_ruta_trabajo = @id_ruta_trabajo)
        THROW 50100, N'La ruta no existe.', 1;

    IF NOT EXISTS (SELECT 1 FROM cliente WHERE id_cliente = @id_cliente)
        THROW 50101, N'El cliente no existe.', 1;

    IF NOT EXISTS (SELECT 1 FROM precio_lugar WHERE id_precio_lugar = @id_precio_lugar)
        THROW 50102, N'El precio/lugar no existe.', 1;

    IF @id_cotizacion IS NOT NULL
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM cotizacion WHERE id_cotizacion = @id_cotizacion AND estado = 'Aprobada')
            THROW 50103, N'La cotización no existe o no está aprobada.', 1;

        IF EXISTS (SELECT 1 FROM servicio_contrato WHERE id_cotizacion = @id_cotizacion)
            THROW 50104, N'Esa cotización ya tiene contrato.', 1;
    END;

    IF @fecha IS NULL
        SET @fecha = CAST(GETDATE() AS DATE);

    INSERT INTO servicio_contrato (
        id_ruta_trabajo, id_cliente, id_precio_lugar,
        observacion, tipo_transportaje, fecha, id_cotizacion
    )
    VALUES (
        @id_ruta_trabajo, @id_cliente, @id_precio_lugar,
        @observacion, @tipo_transportaje, @fecha, @id_cotizacion
    );

    DECLARE @id INT = SCOPE_IDENTITY();

    UPDATE ruta_trabajo
    SET id_servicio_contrato = @id
    WHERE id_ruta_trabajo = @id_ruta_trabajo
      AND id_servicio_contrato IS NULL;

    SELECT @id AS id_servicio_contrato;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_RegistrarServicioExterno
    @id_administrador INT,
    @nombre NVARCHAR(140),
    @cantidad_pago DECIMAL(12,2),
    @direccion NVARCHAR(250) = NULL,
    @fecha DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (
        SELECT 1 FROM empleado
        WHERE id_empleado = @id_administrador
          AND tipo IN ('AdministradorOperativo', 'AdministradorTecnico')
          AND estado = 'Activo'
    )
        THROW 50110, N'El administrador no existe o no está activo.', 1;

    IF NULLIF(LTRIM(RTRIM(@nombre)), '') IS NULL
        THROW 50111, N'El nombre del servicio es obligatorio.', 1;

    IF @cantidad_pago < 0
        THROW 50112, N'La cantidad de pago no puede ser negativa.', 1;

    DECLARE @texto NVARCHAR(150) = LTRIM(RTRIM(@nombre));
    IF @texto NOT LIKE N'\[____-__\]%' ESCAPE N'\'
        SET @texto = N'[' + CONVERT(CHAR(7), COALESCE(@fecha, CAST(GETDATE() AS DATE)), 23) + N'] ' + @texto;

    INSERT INTO servicio_externo (id_administrador, nombre, cantidad_pago, direccion)
    VALUES (@id_administrador, @texto, @cantidad_pago, @direccion);

    SELECT CAST(SCOPE_IDENTITY() AS INT) AS id_servicio_ext;
END;
GO

INSERT INTO empleado (codigo_empleado, nombre, dui, telefono, sueldo, fecha_ingreso, estado, tipo)
VALUES
    (N'710245', N'Laura Beatriz Campos',   N'10000001-1', N'7000-1001', 950.00, '2024-01-15', N'Activo', N'AdministradorOperativo'),
    (N'820376', N'Ricardo Antonio Molina', N'10000002-2', N'7000-1002', 900.00, '2024-02-01', N'Activo', N'AdministradorTecnico'),
    (N'3104',   N'Carlos Eduardo Rivas',   N'10000003-3', N'7000-1003', 600.00, '2024-03-10', N'Activo', N'Tecnico'),
    (N'4215',   N'Mario José Hernández',   N'10000004-4', N'7000-1004', 650.00, '2024-04-05', N'Activo', N'Motorista');
GO

INSERT INTO camion (placa, nombre, tipo, estado)
VALUES (N'C123456', N'Volqueta 1', N'Volqueta', N'Disponible');
GO

SELECT codigo_empleado, nombre, tipo FROM empleado ORDER BY id_empleado;
GO
