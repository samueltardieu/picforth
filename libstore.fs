\
\ PicForth library file
\
\ This library file has been written by Samuel Tardieu <sam@rfc1149.net>.
\ It belongs to the public domain. Do whatever you want with it.
\

variable tmp3

macro
: tmp3>w tmp3 @ >w ;
: w>tmp3 w> tmp3 ! ;
target

: any-!
    suspend-interrupts
    popw w>tmp1 popw w>tmp2 fsr>w w>tmp3 tmp1>w w>fsr tmp2>w storew
    tmp3>w w>fsr
    restore-interrupts
;

forth> ' any-! (!)-loaded !
