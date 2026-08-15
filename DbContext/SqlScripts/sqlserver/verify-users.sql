USE [sql-music];

-- Show database users
SELECT 
    dp.name AS PrincipalName,
    dp.type_desc AS PrincipalType,
    dp.authentication_type_desc AS AuthenticationType,
    dp.create_date,
    dp.modify_date
FROM sys.database_principals dp
WHERE dp.type NOT IN ('A', 'G', 'R', 'X') -- Exclude application roles, Windows groups, database roles, and external groups
ORDER BY dp.name;

-- Show server logins
SELECT 
    sp.name AS LoginName,
    sp.type_desc AS LoginType,
    sp.create_date,
    sp.modify_date,
    sp.is_disabled,
    sp.type
FROM sys.server_principals sp
WHERE sp.type IN ('S') AND sp.is_disabled = 0 -- Show enabled SQL Logins
ORDER BY sp.name;

-- Show database role memberships for users
SELECT 
    dp.name AS PrincipalName,
    r.name AS RoleName
FROM sys.database_role_members rm
JOIN sys.database_principals dp ON rm.member_principal_id = dp.principal_id
JOIN sys.database_principals r ON rm.role_principal_id = r.principal_id
WHERE dp.name IN ('gstusrUser', 'usrUser', 'supusrUser', 'dboUser')
ORDER BY dp.name, r.name;

-- Show database permissions for users
SELECT 
    dp.name AS PrincipalName,
    p.permission_name,
    p.state_desc AS PermissionState,
    p.class_desc AS PermissionClass,
    CASE 
        WHEN p.class = 1 THEN OBJECT_NAME(p.major_id)
        WHEN p.class = 3 THEN SCHEMA_NAME(p.major_id)
        ELSE 'DATABASE'
    END AS ObjectName
FROM sys.database_permissions p
JOIN sys.database_principals dp ON p.grantee_principal_id = dp.principal_id
WHERE dp.name IN ('gstusrUser', 'usrUser', 'supusrUser', 'dboUser', 'gstUsrRole', 'usrRole', 'supUsrRole')
ORDER BY dp.name, p.permission_name;