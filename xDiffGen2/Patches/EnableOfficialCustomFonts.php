<?php
    function EnableOfficialCustomFonts($exe) {
        if ($exe === true) {
            return new xPatch(17, 'Enable Official Custom Fonts', 'UI', 0, 'This option forces Official Custom Fonts (eot files int data folder) on all langtype.');
        }
        $code =  "\x0F\x85\xAE\x00\x00\x00"
                ."\xE8\xAB\xAB\xAB\xFF";
        $offset = $exe->code($code, "\xAB");
        if ($offset === false) {
            echo "Failed in part 1";
            return false;
        }
        $exe->replace($offset, array(0 => "\x90\x90\x90\x90\x90\x90"));

        return true;
    }
?>