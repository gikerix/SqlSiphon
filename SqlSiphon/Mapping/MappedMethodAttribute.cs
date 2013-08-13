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
using System.Data;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SqlSiphon.Mapping
{
    /// <summary>
    /// An attribute to use for tagging methods as being mapped to a stored procedure call.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class MappedMethodAttribute : MappedSchemaObjectAttribute
    {
        public int Timeout = -1;
        public CommandType CommandType = CommandType.StoredProcedure;
        public string Query;
        public bool EnableTransaction = false;
        public bool ReturnsMany = false;

        /// <summary>
        /// ReturnType is only supported by Postgres at this time
        /// </summary>
        public MappedTypeAttribute ReturnType;

        public List<MappedParameterAttribute> Parameters { get; private set; }
        public MappedMethodAttribute()
        {
            this.Parameters = new List<MappedParameterAttribute>();
        }

        public void SetInfo(MethodInfo method, string defaultSchemaName, Func<MappedTypeAttribute, string> toReturnSqlType)
        {
            this.Schema = this.Schema ?? defaultSchemaName;
            this.Name = this.Name ?? method.Name;

            if (this.ReturnType == null || this.ReturnType.SqlType == null)
            {
                this.ReturnType = this.ReturnType
                    ?? method
                    .GetCustomAttributes(typeof(MappedTypeAttribute), false)
                    .Cast<MappedTypeAttribute>()
                    .FirstOrDefault();

                if (this.ReturnType == null)
                    this.ReturnType = new MappedTypeAttribute();

                if (this.ReturnType.SystemType == null)
                    this.ReturnType.SetSystemType(method.ReturnType, toReturnSqlType);
            }

            foreach (var parameter in method.GetParameters())
            {
                AddParameter(parameter, toReturnSqlType);
            }
        }

        private void AddParameter(ParameterInfo parameter, Func<MappedTypeAttribute, string> toSqlStringType)
        {
            var attr = GetAttribute<MappedParameterAttribute>(parameter);
            if (attr == null)
            {
                attr = new MappedParameterAttribute();
            }
            attr.SetInfo(parameter, toSqlStringType);
            this.Parameters.Add(attr);
        }
    }
}