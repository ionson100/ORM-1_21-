﻿using System;
using System.Collections.Generic;
using System.Data;


namespace ORM_1_21_
{
    interface IServiceSessions
    {
        IDbCommand CommandForLinq { get; }
        object Locker { get; }
       
    }
}