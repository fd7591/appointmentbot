-- ============================================================
-- Script de creación de tablas — Bot Facturación
-- SQL Server 2019+ / Azure SQL Database
--
-- Ejecutar en el orden indicado.
-- El proyecto también puede generar las tablas automáticamente
-- mediante EF Core Migrations con: dotnet ef database update
-- ============================================================

USE BotFacturacion;
GO

-- ─────────────────────────────────────────────────────────────
-- 1. Doctores
-- ─────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Doctores')
BEGIN
    CREATE TABLE Doctores (
        Id          INT IDENTITY(1,1) PRIMARY KEY,
        Nombre      NVARCHAR(200) NOT NULL,
        Especialidad NVARCHAR(100) NOT NULL,
        Activo      BIT NOT NULL DEFAULT 1
    );

    -- Doctor de ejemplo
    INSERT INTO Doctores (Nombre, Especialidad, Activo)
    VALUES ('Dr. Juan García', 'Medicina General', 1);

    PRINT 'Tabla Doctores creada.';
END
GO

-- ─────────────────────────────────────────────────────────────
-- 2. Disponibilidades (horarios del consultorio)
-- ─────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Disponibilidades')
BEGIN
    CREATE TABLE Disponibilidades (
        Id          INT IDENTITY(1,1) PRIMARY KEY,
        DiaSemana   INT NOT NULL,         -- 0=Dom, 1=Lun, ..., 6=Sáb
        HoraInicio  TIME NOT NULL,
        HoraFin     TIME NOT NULL,
        MaxCitas    INT NOT NULL DEFAULT 1,
        Activo      BIT NOT NULL DEFAULT 1
    );

    CREATE INDEX IX_Disponibilidades_Dia ON Disponibilidades (DiaSemana, Activo);

    -- Horarios Lunes a Viernes, 09:00 – 12:00 (bloques de 30 min)
    INSERT INTO Disponibilidades (DiaSemana, HoraInicio, HoraFin, MaxCitas, Activo) VALUES
    -- Lunes
    (1, '09:00', '09:30', 1, 1), (1, '09:30', '10:00', 1, 1), (1, '10:00', '10:30', 1, 1),
    (1, '10:30', '11:00', 1, 1), (1, '11:00', '11:30', 1, 1), (1, '11:30', '12:00', 1, 1),
    -- Martes
    (2, '09:00', '09:30', 1, 1), (2, '09:30', '10:00', 1, 1), (2, '10:00', '10:30', 1, 1),
    (2, '10:30', '11:00', 1, 1), (2, '11:00', '11:30', 1, 1), (2, '11:30', '12:00', 1, 1),
    -- Miércoles
    (3, '09:00', '09:30', 1, 1), (3, '09:30', '10:00', 1, 1), (3, '10:00', '10:30', 1, 1),
    (3, '10:30', '11:00', 1, 1), (3, '11:00', '11:30', 1, 1), (3, '11:30', '12:00', 1, 1),
    -- Jueves
    (4, '09:00', '09:30', 1, 1), (4, '09:30', '10:00', 1, 1), (4, '10:00', '10:30', 1, 1),
    (4, '10:30', '11:00', 1, 1), (4, '11:00', '11:30', 1, 1), (4, '11:30', '12:00', 1, 1),
    -- Viernes
    (5, '09:00', '09:30', 1, 1), (5, '09:30', '10:00', 1, 1), (5, '10:00', '10:30', 1, 1),
    (5, '10:30', '11:00', 1, 1), (5, '11:00', '11:30', 1, 1), (5, '11:30', '12:00', 1, 1);

    PRINT 'Tabla Disponibilidades creada con horarios de ejemplo.';
END
GO

-- ─────────────────────────────────────────────────────────────
-- 3. Citas
-- ─────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Citas')
BEGIN
    CREATE TABLE Citas (
        Id                  INT IDENTITY(1,1) PRIMARY KEY,
        PacienteNombre      NVARCHAR(200) NOT NULL,
        PacienteTelefono    NVARCHAR(20)  NOT NULL,
        FechaCita           DATE          NOT NULL,
        HoraCita            TIME          NOT NULL,
        Motivo              NVARCHAR(500) NOT NULL,
        Estado              NVARCHAR(20)  NOT NULL DEFAULT 'Confirmada',
        FolioConfirmacion   NVARCHAR(30)  NOT NULL,
        CreadoEn            DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
        RecordatorioEnviado BIT           NOT NULL DEFAULT 0,
        DoctorId            INT           NULL REFERENCES Doctores(Id) ON DELETE SET NULL
    );

    CREATE INDEX IX_Citas_Telefono        ON Citas (PacienteTelefono);
    CREATE UNIQUE INDEX UQ_Citas_Folio    ON Citas (FolioConfirmacion);
    CREATE INDEX IX_Citas_FechaEstado     ON Citas (FechaCita, Estado);
    CREATE INDEX IX_Citas_Recordatorio   ON Citas (RecordatorioEnviado, FechaCita);

    PRINT 'Tabla Citas creada.';
END
GO

-- ─────────────────────────────────────────────────────────────
-- 4. ReceptoresFiscales
-- ─────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ReceptoresFiscales')
BEGIN
    CREATE TABLE ReceptoresFiscales (
        Id                       INT IDENTITY(1,1) PRIMARY KEY,
        RFC                      NVARCHAR(13)  NOT NULL,
        RazonSocial              NVARCHAR(300) NOT NULL,
        RegimenFiscalClave       NVARCHAR(5)   NOT NULL,
        RegimenFiscalDescripcion NVARCHAR(200) NOT NULL,
        CodigoPostal             NVARCHAR(5)   NOT NULL,
        Email                    NVARCHAR(200) NOT NULL,
        TelefonoRegistro         NVARCHAR(20)  NOT NULL,
        CreadoEn                 DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
        ActualizadoEn            DATETIME2     NOT NULL DEFAULT GETUTCDATE()
    );

    CREATE UNIQUE INDEX UQ_ReceptoresFiscales_RFC ON ReceptoresFiscales (RFC);

    PRINT 'Tabla ReceptoresFiscales creada.';
END
GO

-- ─────────────────────────────────────────────────────────────
-- 5. SolicitudesFactura
-- ─────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SolicitudesFactura')
BEGIN
    CREATE TABLE SolicitudesFactura (
        Id                   INT IDENTITY(1,1) PRIMARY KEY,
        Folio                NVARCHAR(30)      NOT NULL,
        TelefonoSolicitante  NVARCHAR(20)      NOT NULL,
        ReceptorFiscalId     INT               NOT NULL REFERENCES ReceptoresFiscales(Id) ON DELETE NO ACTION,
        FechaConsulta        DATE              NOT NULL,
        Monto                DECIMAL(10,2)     NOT NULL,
        MetodoPagoClave      NVARCHAR(5)       NOT NULL,
        MetodoPagoDescripcion NVARCHAR(100)    NOT NULL,
        FormaPago            NVARCHAR(3)       NOT NULL,
        UsoCFDIClave         NVARCHAR(5)       NOT NULL,
        UsoCFDIDescripcion   NVARCHAR(200)     NOT NULL,
        Estado               NVARCHAR(20)      NOT NULL DEFAULT 'Pendiente',
        NotasContador        NVARCHAR(500)     NULL,
        FolioFiscalUUID      NVARCHAR(50)      NULL,
        FilaGoogleSheets     INT               NULL,
        CreadoEn             DATETIME2         NOT NULL DEFAULT GETUTCDATE(),
        ActualizadoEn        DATETIME2         NOT NULL DEFAULT GETUTCDATE()
    );

    CREATE UNIQUE INDEX UQ_SolicitudesFactura_Folio ON SolicitudesFactura (Folio);
    CREATE INDEX IX_SolicitudesFactura_Telefono     ON SolicitudesFactura (TelefonoSolicitante);
    CREATE INDEX IX_SolicitudesFactura_Estado       ON SolicitudesFactura (Estado);

    PRINT 'Tabla SolicitudesFactura creada.';
END
GO

-- ─────────────────────────────────────────────────────────────
-- 6. ConversationStates
-- ─────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ConversationStates')
BEGIN
    CREATE TABLE ConversationStates (
        Id           INT IDENTITY(1,1) PRIMARY KEY,
        PhoneNumber  NVARCHAR(20)   NOT NULL,
        CurrentFlow  NVARCHAR(30)   NOT NULL DEFAULT 'None',
        CurrentStep  NVARCHAR(50)   NOT NULL DEFAULT 'None',
        TempData     NVARCHAR(MAX)  NULL,
        UpdatedAt    DATETIME2      NOT NULL DEFAULT GETUTCDATE(),
        CreatedAt    DATETIME2      NOT NULL DEFAULT GETUTCDATE()
    );

    CREATE UNIQUE INDEX UQ_ConversationStates_Phone ON ConversationStates (PhoneNumber);
    CREATE INDEX IX_ConversationStates_UpdatedAt    ON ConversationStates (UpdatedAt);

    PRINT 'Tabla ConversationStates creada.';
END
GO

-- ─────────────────────────────────────────────────────────────
-- Verificación final
-- ─────────────────────────────────────────────────────────────
SELECT
    t.name AS Tabla,
    p.rows AS Filas
FROM sys.tables t
JOIN sys.partitions p ON t.object_id = p.object_id AND p.index_id IN (0,1)
WHERE t.name IN ('Doctores','Disponibilidades','Citas','ReceptoresFiscales','SolicitudesFactura','ConversationStates')
ORDER BY t.name;
GO

PRINT '=== Script ejecutado correctamente ===';
GO
