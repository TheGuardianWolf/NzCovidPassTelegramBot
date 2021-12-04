docker build -t nzpasslink:heroku -f Dockerfile.heroku .
docker tag nzpasslink:heroku registry.heroku.com/nzpasslink/web
docker push registry.heroku.com/nzpasslink/web
heroku container:release web -a nzpasslink