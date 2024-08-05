Feature: Customer
Scenario: Customer with invalid or expired token send a request to ApiGateway

	Given customer with invalid or expired token
	When customer send a request to ApiGateway
	Then It return code 401

Scenario: Customer with valid token send a request to ApiGateway

	Given customer with valid token
	When customer send a request to ApiGateway
	Then It send a request to api