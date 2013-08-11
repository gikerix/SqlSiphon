﻿/*
https://www.github.com/capnmidnight/SqlSiphon
Copyright (c) 2009, 2010, 2011, 2012, 2013 Sean T. McBeth
All rights reserved.

Redistribution and use in source and binary forms, with or without modification, 
are permitted provided that the following conditions are met:

* Redistributions of source code must retain the above copyright notice, this 
  list of conditions and the following disclaimer.

* Redistributions in binary form must reproduce the above copyright notice, this 
  list of conditions and the following disclaimer in the documentation and/or 
  other materials provided with the distribution.

* Neither the name of McBeth Software Systems nor the names of its contributors
  may be used to endorse or promote products derived from this software without 
  specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND 
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED 
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. 
IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, 
INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, 
BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, 
DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF 
LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE 
OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED 
OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using System;
using System.Collections.Generic;
using System.Linq;

namespace SqlSiphon.Mapping
{
    /// <summary>
    /// An attribute to use for tagging methods as being mapped to a stored procedure call.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Enum, Inherited = true, AllowMultiple = false)]
    public class MappedClassAttribute : MappedSchemaObjectAttribute
    {
        public bool IsHistoryTracked = false;
        public string PreAddConstraintScript;
        public string PostAddConstraintScript;
        public bool IncludeInSynch = true;

        private List<MappedMethodAttribute> methods;
        private List<MappedPropertyAttribute> properties;
        
        public MappedClassAttribute()
        {
            this.methods = new List<MappedMethodAttribute>();
            this.properties = new List<MappedPropertyAttribute>();
        }

        public void SetInfo(Type type, string defaultSchemaName)
        {
            base.SetInfo(type);

            foreach (var method in type.GetMethods())
            {
                MaybeAddMethod(method, defaultSchemaName);
            }

            foreach (var property in type.GetProperties())
            {
                MaybeAddProperty(property);
            }
        }

        private void MaybeAddMethod(System.Reflection.MethodInfo method, string defaultSchemaName)
        {
            var attr = GetAttribute<MappedMethodAttribute>(method);
            if (attr != null)
            {
                attr.SetInfo(method, defaultSchemaName);
                this.methods.Add(attr);
            }
        }

        private void MaybeAddProperty(System.Reflection.PropertyInfo property)
        {
            var attr = GetAttribute<MappedPropertyAttribute>(property);
            if (attr != null)
            {
                attr.SetInfo(property);
                this.properties.Add(attr);
            }
        }
    }
}
