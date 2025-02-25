using System;
using MongoDB.Bson.Serialization;

namespace NutritionAmbition.Backend.API.Models
{
    public class GuidAsStringGenerator : IIdGenerator
        {
            public object GenerateId(object container, object document)
            {
                return Guid.NewGuid().ToString();
            }

            public bool IsEmpty(object id)
            {
                return id == null;
            }
        }
}