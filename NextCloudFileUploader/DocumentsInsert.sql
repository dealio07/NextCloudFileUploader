DROP TABLE IF EXISTS [dbo].[DODocuments];
CREATE TABLE [dbo].[DODocuments] (
	Number INT IDENTITY (0, 1) PRIMARY KEY NOT NULL, 
	Entity VARCHAR(50) NOT NULL, 
	EntityId UNIQUEIDENTIFIER NOT NULL, 
	FileId UNIQUEIDENTIFIER NOT NULL, 
	Version INT NOT NULL
);

INSERT INTO [dbo].[DODocuments] (Entity, EntityId, FileId, Version)
SELECT 'AccountFile' AS Entity, af.AccountId, af.Id, af.Version FROM [dbo].[AccountFile] af WITH (NOLOCK)
WHERE af.AccountId IS NOT NULL AND DATALENGTH(af.Data) > 0
ORDER BY af.AccountId, af.Id;

INSERT INTO [dbo].[DODocuments] (Entity, EntityId, FileId, Version)
SELECT 'ContactFile' AS Entity, cf.ContactId, cf.Id, cf.Version FROM [dbo].[ContactFile] cf WITH (NOLOCK)
WHERE cf.ContactId IS NOT NULL AND DATALENGTH(cf.Data) > 0
ORDER BY cf.ContactId, cf.Id;

INSERT INTO [dbo].[DODocuments] (Entity, EntityId, FileId, Version)
SELECT 'ContractFile' AS Entity, f.ContractId, f.Id, fv.PTVersion FROM [dbo].[ContractFile] f WITH (NOLOCK)
INNER JOIN [dbo].[PTFileVersion] fv ON f.ContractId IS NOT NULL 
									AND fv.PTVersion IS NOT NULL 
									AND fv.PTFile = f.Id 
									AND DATALENGTH(fv.PTData) > 0
ORDER BY f.ContractId, f.Id;