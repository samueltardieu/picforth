\ Provides jump table primitives
\ Implemented by David McNab <david@rebirthing.co.nz>

host

: element-jtable ( xt -- ) goto ;

meta

: jtable ( "name" -- start endxt elemxt )

    reachable tcshere ['] end-ftable ['] element-jtable

    \ need to save index on stack, hence the ':' instead of '::'
    meta> :

    \ frig to insert instruction to set pclath, so tables located
    \ outside first 256 bytes of prog mem will work
    tcshere $100 / movlw
    pclath         movwf

    \ now can pull index off stack
    meta> >w

    return-in-w pcl ,f addwf
    -tcompile
;

target
