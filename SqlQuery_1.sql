USE InsuranceDatabase

DECLARE @docId INT = 9735;

SELECT Er.ErrorId
	,Er.RuleId
	,Er.Scope
	,Er.rowCol
	,Er.TableBaseFormula
	,Er.Filter
	,Er.RuleMessage
	,Er.IsError
	,Er.IsWarning
	,er.IsDataError
	,Er.DataValue
FROM dbo.ERROR_Rule Er
WHERE Er.ErrorDocumentId = @docId
ORDER BY er.RuleId
	,Er.Scope
	,Er.rowCol
