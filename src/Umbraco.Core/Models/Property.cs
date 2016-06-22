﻿using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Umbraco.Core.Models.EntityBase;

namespace Umbraco.Core.Models
{
    /// <summary>
    /// A Property contains a single piece of data
    /// </summary>
    [Serializable]
    [DataContract(IsReference = true)]
    public class Property : Entity
    {
        private PropertyType _propertyType;
        private Guid _version;
        private object _value;
        private readonly PropertyTags _tagSupport = new PropertyTags();

        protected Property()
        {
            
        }

        public Property(PropertyType propertyType)
        {
            _propertyType = propertyType;
        }

        public Property(PropertyType propertyType, object value)
        {
            _propertyType = propertyType;
            Value = value;
        }

        public Property(int id, Guid version, PropertyType propertyType, object value)
        {
            Id = id;
            _propertyType = propertyType;
            _version = version;
            Value = value;
        }

        private static readonly Lazy<PropertySelectors> Ps = new Lazy<PropertySelectors>();

        private class PropertySelectors
        {
            public readonly PropertyInfo ValueSelector = ExpressionHelper.GetPropertyInfo<Property, object>(x => x.Value);
            public readonly PropertyInfo VersionSelector = ExpressionHelper.GetPropertyInfo<Property, Guid>(x => x.Version);
        }

        private static readonly DelegateEqualityComparer<object> ValueComparer = new DelegateEqualityComparer<object>(
            (o, o1) =>
            {
                if (o == null && o1 == null) return true;

                //custom comparer for strings.                        
                if (o is string || o1 is string)
                {
                    //if one is null and another is empty then they are the same
                    if ((o as string).IsNullOrWhiteSpace() && (o1 as string).IsNullOrWhiteSpace())
                    {
                        return true;
                    }
                    if (o == null || o1 == null) return false;
                    return o.Equals(o1);
                }

                if (o == null || o1 == null) return false;

                //Custom comparer for enumerable if it is enumerable
                var enum1 = o as IEnumerable;
                var enum2 = o1 as IEnumerable;
                if (enum1 != null && enum2 != null)
                {
                    return enum1.Cast<object>().UnsortedSequenceEqual(enum2.Cast<object>());
                }
                return o.Equals(o1);
            }, o => o.GetHashCode());
        
        /// <summary>
        /// Returns the instance of the tag support, by default tags are not enabled
        /// </summary>
        internal PropertyTags TagSupport
        {
            get { return _tagSupport; }
        }

        /// <summary>
        /// Returns the Alias of the PropertyType, which this Property is based on
        /// </summary>
        [DataMember]
        public string Alias { get { return _propertyType.Alias; } }

        /// <summary>
        /// Returns the Id of the PropertyType, which this Property is based on
        /// </summary>
        [IgnoreDataMember]
        internal int PropertyTypeId { get { return _propertyType.Id; } }

        /// <summary>
        /// Returns the DatabaseType that the underlaying DataType is using to store its values
        /// </summary>
        /// <remarks>
        /// Only used internally when saving the property value.
        /// </remarks>
        [IgnoreDataMember]
        internal DataTypeDatabaseType DataTypeDatabaseType
        {
            get { return _propertyType.DataTypeDatabaseType; }
        }

        /// <summary>
        /// Returns the PropertyType, which this Property is based on
        /// </summary>
        [IgnoreDataMember]
        public PropertyType PropertyType { get { return _propertyType; } }
        
        /// <summary>
        /// Gets or Sets the version id for the Property
        /// </summary>
        /// <remarks>
        /// The version will be the same for all Property objects in a collection on a Content 
        /// object, so not sure how much this makes sense but adding it to align with:
        /// umbraco.interfaces.IProperty
        /// </remarks>
        [DataMember]
        public Guid Version
        {
            get { return _version; }
            set { SetPropertyValueAndDetectChanges(value, ref _version, Ps.Value.VersionSelector); }
        }

        /// <summary>
        /// Gets or Sets the value of the Property
        /// </summary>
        /// <remarks>
        /// Setting the value will trigger a type validation. 
        /// The type of the value has to be valid in order to be saved.
        /// </remarks>
        [DataMember]
        public object Value
        {
            get { return _value; }
            set
            {
                bool typeValidation = _propertyType.IsPropertyTypeValid(value);

                if (typeValidation == false)
                {
                    // Normally we'll throw an exception here.  However if the property is of a type that can have it's data field (dataInt, dataVarchar etc.)
                    // changed, we might have a value of the now "wrong" type.  As of May 2016 Label is the only built-in property editor that supports this.
                    // In that case we should try to parse the value and return null if that's not possible rather than throwing an exception.
                    if (value != null && _propertyType.CanHaveDataValueTypeChanged())
                    {
                        var stringValue = value.ToString();
                        switch (_propertyType.DataTypeDatabaseType)
                        {
                            case DataTypeDatabaseType.Nvarchar:
                            case DataTypeDatabaseType.Ntext:
                                value = stringValue;
                                break;
                            case DataTypeDatabaseType.Integer:
                                int integerValue;
                                if (int.TryParse(stringValue, out integerValue) == false)
                                {
                                    // Edge case, but if changed from decimal --> integer, the above tryparse will fail.  So we'll try going
                                    // via decimal too to return the integer value rather than zero.
                                    decimal decimalForIntegerValue;
                                    if (decimal.TryParse(stringValue, out decimalForIntegerValue))
                                    {
                                        integerValue = (int)decimalForIntegerValue;
                                    }
                                }

                                value = integerValue;
                                break;
                            case DataTypeDatabaseType.Decimal:
                                decimal decimalValue;
                                decimal.TryParse(stringValue, out decimalValue);
                                value = decimalValue;
                                break;
                            case DataTypeDatabaseType.Date:
                                DateTime dateValue;
                                DateTime.TryParse(stringValue, out dateValue);
                                value = dateValue;
                                break;
                        }
                    }
                    else
                    {
                        throw new Exception(
                            string.Format(
                                "Type validation failed. The value type: '{0}' does not match the DataType in PropertyType with alias: '{1}'",
                                value == null ? "null" : value.GetType().Name, Alias));
                    }
                }

                SetPropertyValueAndDetectChanges(value, ref _value, Ps.Value.ValueSelector, ValueComparer);
            }
        }

        /// <summary>
        /// Boolean indicating whether the current value is valid
        /// </summary>
        /// <remarks>
        /// A valid value implies that it is ready for publishing.
        /// Invalid property values can be saved, but not published.
        /// </remarks>
        /// <returns>True is property value is valid, otherwise false</returns>
        public bool IsValid()
        {
            return IsValid(Value);
        }

        /// <summary>
        /// Boolean indicating whether the passed in value is valid
        /// </summary>
        /// <param name="value"></param>
        /// <returns>True is property value is valid, otherwise false</returns>
        public bool IsValid(object value)
        {
            return _propertyType.IsPropertyValueValid(value);
        }

        public override object DeepClone()
        {
            var clone = (Property)base.DeepClone();
            //turn off change tracking
            clone.DisableChangeTracking();
            //need to manually assign since this is a readonly property
            clone._propertyType = (PropertyType)PropertyType.DeepClone();
            //this shouldn't really be needed since we're not tracking
            clone.ResetDirtyProperties(false);
            //re-enable tracking
            clone.EnableChangeTracking();
            
            return clone;
        }
    }
}