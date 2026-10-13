USE sacor;
GO

IF OBJECT_ID('dbo.codigo_activacion_movil', 'U') IS NULL
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

IF OBJECT_ID('dbo.sesion_movil', 'U') IS NULL
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

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_sesion_movil_empleado')
CREATE INDEX IX_sesion_movil_empleado ON sesion_movil (id_empleado, revocado_utc);
GO

IF OBJECT_ID('dbo.evidencia_foto', 'U') IS NULL
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

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_evidencia_foto_limpieza')
CREATE INDEX IX_evidencia_foto_limpieza ON evidencia_foto (id_limpieza);
GO

IF COL_LENGTH('dbo.ruta_trabajo', 'id_servicio_contrato') IS NULL
    ALTER TABLE ruta_trabajo ADD id_servicio_contrato INT NULL;
GO

IF COL_LENGTH('dbo.ruta_trabajo', 'id_camion') IS NULL
    ALTER TABLE ruta_trabajo ADD id_camion INT NULL;
GO

IF COL_LENGTH('dbo.servicio_contrato', 'id_cotizacion') IS NULL
    ALTER TABLE servicio_contrato ADD id_cotizacion INT NULL;
GO

IF COL_LENGTH('dbo.limpieza', 'id_ruta_trabajo') IS NULL
    ALTER TABLE limpieza ADD id_ruta_trabajo INT NULL;
GO

IF EXISTS (SELECT 1 FROM sys.columns
           WHERE object_id = OBJECT_ID('dbo.limpieza') AND name = 'id_servicio_contrato' AND is_nullable = 0)
BEGIN
    IF OBJECT_ID('dbo.FK_limpieza_contrato', 'F') IS NOT NULL
        ALTER TABLE limpieza DROP CONSTRAINT FK_limpieza_contrato;
    ALTER TABLE limpieza ALTER COLUMN id_servicio_contrato INT NULL;
    ALTER TABLE limpieza ADD CONSTRAINT FK_limpieza_contrato FOREIGN KEY (id_servicio_contrato)
        REFERENCES servicio_contrato(id_servicio_contrato);
END
GO

IF COL_LENGTH('dbo.ruta_trabajo', 'id_cotizacion') IS NOT NULL
BEGIN
    EXEC sp_executesql N'
        UPDATE s SET s.id_cotizacion = r.id_cotizacion
        FROM servicio_contrato s
        JOIN ruta_trabajo r ON r.id_ruta_trabajo = s.id_ruta_trabajo
        WHERE r.id_cotizacion IS NOT NULL AND s.id_cotizacion IS NULL;

        UPDATE r SET r.id_servicio_contrato = s.id_servicio_contrato
        FROM ruta_trabajo r
        JOIN servicio_contrato s ON s.id_cotizacion = r.id_cotizacion
        WHERE r.id_cotizacion IS NOT NULL AND r.id_servicio_contrato IS NULL;';

    IF OBJECT_ID('dbo.FK_ruta_cotizacion', 'F') IS NOT NULL
        ALTER TABLE ruta_trabajo DROP CONSTRAINT FK_ruta_cotizacion;
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ruta_trabajo_cotizacion')
        DROP INDEX IX_ruta_trabajo_cotizacion ON ruta_trabajo;
    ALTER TABLE ruta_trabajo DROP COLUMN id_cotizacion;
END
GO

IF OBJECT_ID('dbo.FK_ruta_contrato', 'F') IS NULL
    ALTER TABLE ruta_trabajo ADD CONSTRAINT FK_ruta_contrato FOREIGN KEY (id_servicio_contrato)
        REFERENCES servicio_contrato(id_servicio_contrato);
GO

IF OBJECT_ID('dbo.FK_ruta_camion', 'F') IS NULL
    ALTER TABLE ruta_trabajo ADD CONSTRAINT FK_ruta_camion FOREIGN KEY (id_camion)
        REFERENCES camion(id_camion);
GO

IF OBJECT_ID('dbo.FK_contrato_cotizacion', 'F') IS NULL
    ALTER TABLE servicio_contrato ADD CONSTRAINT FK_contrato_cotizacion FOREIGN KEY (id_cotizacion)
        REFERENCES cotizacion(id_cotizacion);
GO

IF OBJECT_ID('dbo.FK_limpieza_ruta', 'F') IS NULL
    ALTER TABLE limpieza ADD CONSTRAINT FK_limpieza_ruta FOREIGN KEY (id_ruta_trabajo)
        REFERENCES ruta_trabajo(id_ruta_trabajo);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ruta_trabajo_contrato')
    CREATE INDEX IX_ruta_trabajo_contrato ON ruta_trabajo (id_servicio_contrato);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_contrato_cotizacion')
    CREATE UNIQUE INDEX UX_contrato_cotizacion ON servicio_contrato (id_cotizacion) WHERE id_cotizacion IS NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_limpieza_ruta')
    CREATE INDEX IX_limpieza_ruta ON limpieza (id_ruta_trabajo);
GO

UPDATE empleado SET tipo = 'AdministradorOperativo' WHERE tipo = 'Administrador';
UPDATE empleado SET estado = 'Inactivo' WHERE tipo IN ('Ayudante', 'Mecanico', 'Coordinador') AND estado = 'Activo';
GO

IF OBJECT_ID('dbo.CK_empleado_tipo', 'C') IS NULL
    ALTER TABLE empleado ADD CONSTRAINT CK_empleado_tipo
        CHECK (tipo IN ('AdministradorOperativo', 'AdministradorTecnico', 'Tecnico', 'Motorista', 'Ayudante', 'Mecanico', 'Coordinador'));
GO

IF OBJECT_ID('dbo.CK_empleado_tipo_anterior', 'C') IS NULL
    ALTER TABLE empleado ADD CONSTRAINT CK_empleado_tipo_anterior
        CHECK (tipo NOT IN ('Ayudante', 'Mecanico', 'Coordinador') OR estado <> 'Activo');
GO

IF COL_LENGTH('dbo.viaje_extra', 'id_ruta_trabajo') IS NULL
    ALTER TABLE viaje_extra ADD id_ruta_trabajo INT NULL;
GO

IF OBJECT_ID('dbo.FK_viaje_extra_ruta', 'F') IS NULL
    ALTER TABLE viaje_extra ADD CONSTRAINT FK_viaje_extra_ruta FOREIGN KEY (id_ruta_trabajo)
        REFERENCES ruta_trabajo(id_ruta_trabajo);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_viaje_extra_ruta')
    CREATE INDEX IX_viaje_extra_ruta ON viaje_extra (id_ruta_trabajo);
GO

IF OBJECT_ID('dbo.gasto_viaje', 'U') IS NULL
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

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_gasto_viaje_ruta')
CREATE INDEX IX_gasto_viaje_ruta ON gasto_viaje (id_ruta_trabajo);
GO

IF COL_LENGTH('dbo.pesaje', 'id_ruta_trabajo') IS NULL
    ALTER TABLE pesaje ADD id_ruta_trabajo INT NULL;
GO

IF OBJECT_ID('dbo.FK_pesaje_ruta', 'F') IS NULL
    ALTER TABLE pesaje ADD CONSTRAINT FK_pesaje_ruta FOREIGN KEY (id_ruta_trabajo)
        REFERENCES ruta_trabajo(id_ruta_trabajo);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_pesaje_ruta')
    CREATE INDEX IX_pesaje_ruta ON pesaje (id_ruta_trabajo);
GO

IF COL_LENGTH('dbo.pesaje', 'precio_tonelada') IS NULL
    ALTER TABLE pesaje ADD precio_tonelada DECIMAL(12,2) NULL;
GO

EXEC sp_executesql N'
    UPDATE p
    SET p.precio_tonelada = COALESCE(
        (SELECT TOP 1 TRY_CAST(CASE WHEN ISJSON(a.valor_anterior) = 1
                                    THEN JSON_VALUE(a.valor_anterior, ''$.precio'') END AS DECIMAL(12,2))
         FROM auditoria a
         WHERE a.tabla_afectada = ''precio_lugar''
           AND a.accion = ''UPDATE''
           AND a.id_registro = CAST(p.id_precio_lugar AS NVARCHAR(100))
           AND a.fecha_hora >= CAST(ISNULL(p.fecha, ''19000101'') AS DATETIME2)
         ORDER BY a.fecha_hora ASC),
        pl.precio)
    FROM pesaje p
    JOIN precio_lugar pl ON pl.id_precio_lugar = p.id_precio_lugar
    WHERE p.precio_tonelada IS NULL;';
GO
