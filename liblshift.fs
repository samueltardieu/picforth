\
\ PicForth library file
\
\ This library file has been written by Samuel Tardieu <sam@rfc1149.net>.
\ It belongs to the public domain. Do whatever you want with it.
\

: any-lshift
    suspend-interrupts
    dup if tmp1 v-for c bit-clr rlf-tos v-next else drop then
    restore-interrupts
;

forth> ' any-lshift (lshift)-loaded !
