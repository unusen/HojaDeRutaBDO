IF COL_LENGTH('dbo.Clientes_Creatio', 'Hdr_Activo') IS NULL ALTER TABLE dbo.Clientes_Creatio ADD Hdr_Activo bit NOT NULL CONSTRAINT DF_Clientes_Creatio_Hdr_Activo DEFAULT (1) WITH VALUES;
