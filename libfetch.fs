\
\ PicForth library file
\
\ This library file has been written by Samuel Tardieu <sam@rfc1149.net>.
\ It belongs to the public domain. Do whatever you want with it.
\

: any-@
    suspend-interrupts
    fsr>w w>tmp1 loadw w>fsr loadw w>tmp2 tmp1>w w>fsr tmp2>w storew
    restore-interrupts
;

forth> ' any-@ (@)-loaded !
