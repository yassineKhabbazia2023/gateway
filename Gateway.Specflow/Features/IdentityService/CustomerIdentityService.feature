Feature: IdentityService verify a customer

Scenario Outline: IdentityService verify a customer <isExists> in Gigya

	Given customer <isExists> in Gigya
	When IdentityService verify this customer
	Then It return code <statusCode>

	Examples: 
	| isExists   | statusCode |
	| exists     | 200        |
	| not exists | 403        |
