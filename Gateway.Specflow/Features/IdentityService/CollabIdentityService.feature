Feature: IdentityService verify a collab

Scenario Outline: IdentityService verify a collab <isExists> in group

	Given collab <isExists> in group
	When IdentityService verify this collab
	Then It return code <statusCode>

	Examples: 
	| isExists   | statusCode |
	| exists     | 200        |
	| not exists | 403        |
