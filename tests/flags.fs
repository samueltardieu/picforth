flag f0
variable var1
flag f1
flag f2
variable var2
flag f3
flag f4
variable var3
flag f5
flag f6
flag f7
flag f8
flag f9
variable var4

main : main
  begin
    f0 high
    1 var1 !
    2 var2 !
    f1 low
    f2 low
    f6 low
    3 var3 !
    f7 high
    f8 high
    f9 low
    4 var4 !
  f9 high? until
;