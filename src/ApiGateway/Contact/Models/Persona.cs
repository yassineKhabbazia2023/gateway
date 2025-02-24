// <copyright file="Persona.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using System.Text.Json.Serialization;

namespace ApiGateway.Contact.Models
{
  public class Persona
  {
    public Persona(int id, string name)
    {
      Id = id;
      Name = name;
    }

    [JsonPropertyName("id")]
    public int Id { get; }

    [JsonPropertyName("name")]
    public string Name { get; }

    [JsonPropertyName("description")]
    public string Description { get; set; }
  }
}
