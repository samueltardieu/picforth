: x ;
0 [if] : t1 x ; [then]
1 [if] : t2 x ; [then]
[ifdef] t1 : t3 x ; [then]
[ifdef] t2 : t4 x ; [then]
[ifundef] t1 : t5 x ; [then]
[ifundef] t2 : t6 x ; [then]

