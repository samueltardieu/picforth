\
\
\ PicForth library file
\
\ This library file has been written by Samuel Tardieu <sam@rfc1149.net>.
\ It belongs to the public domain. Do whatever you want with it.
\

variable cmove-src
variable cmove-dst

:: any-cmove ( src dst length -- )
    >w suspend-interrupts w>tmp1   \ suspend-interrupts does not touch W
    cmove-dst ! cmove-src ! tmp1>w w>
    begin
	dup while
	cmove-src @ @ cmove-dst @ ! 1 cmove-src +! 1 cmove-dst +!
    1- repeat drop
    restore-interrupts
;

forth> ' any-cmove (cmove)-loaded !
