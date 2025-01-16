# Bulk Download Photos form Lillio

Downloads all photos from daycare entries and also preserves the date they were take in file date and exif data.

## Steps


1. Go to the "Entries" tab for your child

    ```none
    https://www.himama.com/accounts/<child-id>/activities>
    ```

2. Open the browser and paste the following into the console

    ```js
    var rows = [...document.querySelectorAll("tbody tr")].map(row => {
        var date = row.querySelector("td:nth-child(2)").innerText
        var title = row.querySelector("td:nth-child(3)").innerText
        var link = row.querySelector("a[download]").href
        return {date,title,link}
    })
    ```
   
   You'll have to do page navigation yourself for each tab and keep copy and pasting out the results.  If someone wants to write the fetch in JS and submit a PR, totally fine, but this still should save a bunch of time.
   
     **Note**: Scraping like this is brittle and subject to change over time

3. Copy the rows (in Chrome, you can use `copy(rows)`) and paste into images.json

4. Run the application
