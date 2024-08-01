Feature: Collab
Scenario: Collab with invalid or expired token send a request to ApiGateway

	Given collab with invalid or expired token
	When collab send a request to ApiGateway
	Then It return code 401

Scenario: Collab with valid token send a request to ApiGateway

	Given collab with valid token
	When collab send a request to ApiGateway
	Then It send a request to api